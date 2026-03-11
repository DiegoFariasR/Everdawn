namespace GameCore.Battle;

/// <summary>
/// A stateful interactive battle session. Exposes a single entry point:
/// <see cref="HandleRequest"/>. Clients send <see cref="BattleRequest"/> messages
/// and receive a <see cref="BattleResponse"/> — no direct session properties are
/// ever queried. This mirrors how the UnityClient would talk to GameCore.
/// </summary>
public class InteractiveBattleSession
{
    private const int SkillMpCost    = 30;
    private const int SoulBurnMpCost = 60;

    private readonly List<BattleUnit> _allUnits;
    private readonly List<BattleUnit> _turnOrder;
    private readonly Dictionary<string, int> _hp;
    private readonly Dictionary<string, int> _mp;
    private readonly Random _rng;
    private readonly List<BattleEvent> _log = [];

    private int _turnIndex;
    private bool _started;
    private bool _isOver;
    private string? _winningTeam;

    public InteractiveBattleSession(BattleSetup setup, int seed)
    {
        _allUnits  = [.. setup.PlayerUnits, .. setup.EnemyUnits];
        _turnOrder = [.. _allUnits.OrderByDescending(u => u.Initiative)];
        _hp        = _allUnits.ToDictionary(u => u.Id, u => u.MaxHp);
        _mp        = _allUnits.ToDictionary(u => u.Id, u => u.MaxMp);
        _rng       = new Random(seed);
    }

    /// <summary>
    /// Process a client request and return the resulting battle state.
    /// The returned <see cref="BattleResponse"/> is the complete picture —
    /// clients must not query session internals directly.
    /// </summary>
    public BattleResponse HandleRequest(BattleRequest request) => request switch
    {
        StartBattleRequest        r => HandleStart(r),
        ResumeFromSnapshotRequest r => HandleResume(r),
        PlayerActionRequest       r => HandlePlayerAction(r),
        _ => throw new ArgumentException($"Unknown request type: {request.GetType().Name}")
    };

    // ── Request handlers ─────────────────────────────────────────────────

    private BattleResponse HandleStart(StartBattleRequest _)
    {
        if (_started) throw new InvalidOperationException("Session already started.");
        _started = true;
        AddEvent("system", "Battle begins!", "start");
        return BuildResponse(AutoAdvance());
    }

    private BattleResponse HandleResume(ResumeFromSnapshotRequest r)
    {
        if (_started) throw new InvalidOperationException("Session already started.");
        _started = true;

        foreach (var us in r.State)
        {
            if (_hp.ContainsKey(us.UnitId)) _hp[us.UnitId] = us.CurrentHp;
            if (_mp.ContainsKey(us.UnitId)) _mp[us.UnitId] = us.CurrentMp;
        }
        _turnIndex = NextAliveIndexAfter(r.LastActorId);

        CheckEnd();
        if (_isOver)
        {
            AddEvent("system", "The battle was already over at this point.", "takeover");
            return BuildResponse([]);
        }

        AddEvent("system", $"You took control at step {r.AtStep}.", "takeover");
        return BuildResponse(AutoAdvance());
    }

    private BattleResponse HandlePlayerAction(PlayerActionRequest r)
    {
        if (!_started) throw new InvalidOperationException("Session not started.");
        if (_isOver)   throw new InvalidOperationException("Battle is over.");
        if (_turnOrder[_turnIndex].Team != "player")
            throw new InvalidOperationException("Not a player turn.");

        var actor  = _turnOrder[_turnIndex];
        var target = _allUnits.FirstOrDefault(u => u.Id == r.TargetId)
                     ?? throw new ArgumentException($"Unknown target: {r.TargetId}");

        var newEvents = new List<BattleEvent>(ExecuteAction(actor, target, r.Action));
        AdvanceTurn();
        newEvents.AddRange(AutoAdvance());
        return BuildResponse(newEvents);
    }

    // ── Response builder ─────────────────────────────────────────────────

    private BattleResponse BuildResponse(IReadOnlyList<BattleEvent> newEvents)
    {
        BattlePendingInput? pending = null;
        if (!_isOver && _turnOrder[_turnIndex].Team == "player")
        {
            var actor = _turnOrder[_turnIndex];
            pending = new BattlePendingInput(
                Actor:          actor,
                CanUseSkill:    _mp[actor.Id] >= SkillMpCost,
                CanUseSoulBurn: _mp[actor.Id] >= SoulBurnMpCost,
                ValidTargets:   _allUnits.Where(u => u.Team != actor.Team && _hp[u.Id] > 0).ToArray()
            );
        }

        return new BattleResponse(
            NewEvents:    newEvents,
            FullLog:      _log,
            State:        _allUnits.Select(u => new UnitState(u.Id, _hp[u.Id], _mp[u.Id], _hp[u.Id] > 0)).ToArray(),
            PendingInput: pending,
            IsOver:       _isOver,
            WinningTeam:  _winningTeam
        );
    }

    // ── Internals ────────────────────────────────────────────────────────

    /// <summary>Auto-resolves enemy turns until it's a player's turn or the battle ends.</summary>
    private IReadOnlyList<BattleEvent> AutoAdvance()
    {
        var produced = new List<BattleEvent>();
        while (!_isOver && _turnOrder[_turnIndex].Team != "player")
        {
            var actor   = _turnOrder[_turnIndex];
            var targets = _allUnits.Where(u => u.Team != actor.Team && _hp[u.Id] > 0).ToList();
            if (targets.Count == 0) { CheckEnd(); break; }

            var target = targets[_rng.Next(targets.Count)];
            produced.AddRange(ExecuteAction(actor, target, PlayerActionType.Attack));
            AdvanceTurn();
        }
        return produced;
    }

    private IReadOnlyList<BattleEvent> ExecuteAction(BattleUnit actor, BattleUnit target, PlayerActionType action)
    {
        var produced = new List<BattleEvent>();

        // Consume MP
        if (action == PlayerActionType.Skill    && _mp[actor.Id] >= SkillMpCost)
            _mp[actor.Id] -= SkillMpCost;
        else if (action == PlayerActionType.SoulBurn && _mp[actor.Id] >= SoulBurnMpCost)
            _mp[actor.Id] -= SoulBurnMpCost;
        else
            action = PlayerActionType.Attack; // fallback if MP drained

        double multiplier = action switch
        {
            PlayerActionType.Skill    => 1.5,
            PlayerActionType.SoulBurn => 2.5,
            _                         => 1.0,
        };

        string actionLabel = action switch
        {
            PlayerActionType.Skill    => "uses Skill on",
            PlayerActionType.SoulBurn => "Soul Burns",
            _                         => "attacks",
        };

        int variance = Math.Max(1, actor.Attack / 5);
        int damage   = (int)(actor.Attack * multiplier) + _rng.Next(-variance, variance + 1);
        _hp[target.Id] = Math.Max(0, _hp[target.Id] - damage);

        produced.Add(AddEvent(actor.Id,
            $"{actor.Name} {actionLabel} {target.Name} for {damage} damage.",
            action == PlayerActionType.SoulBurn ? "soulburn" : action == PlayerActionType.Skill ? "skill" : "attack",
            target.Id, damage));

        if (_hp[target.Id] <= 0)
            produced.Add(AddEvent(target.Id, $"{target.Name} is defeated!", "death"));

        CheckEnd();
        return produced;
    }

    private void AdvanceTurn()
    {
        if (_isOver) return;
        for (int i = 0; i < _turnOrder.Count; i++)
        {
            _turnIndex = (_turnIndex + 1) % _turnOrder.Count;
            if (_hp[_turnOrder[_turnIndex].Id] > 0) return;
        }
        CheckEnd();
    }

    private void CheckEnd()
    {
        if (_isOver) return;
        bool playerAlive = _allUnits.Any(u => u.Team == "player" && _hp[u.Id] > 0);
        bool enemyAlive  = _allUnits.Any(u => u.Team == "enemy"  && _hp[u.Id] > 0);
        if (!playerAlive || !enemyAlive)
        {
            _isOver      = true;
            _winningTeam = playerAlive ? "player" : "enemy";
            AddEvent("system", playerAlive ? "Victory!" : "Defeat...", "end");
        }
    }

    private int NextAliveIndexAfter(string? actorId)
    {
        int lastIdx = actorId != null ? _turnOrder.FindIndex(u => u.Id == actorId) : -1;
        int start   = lastIdx >= 0 ? (lastIdx + 1) % _turnOrder.Count : 0;
        for (int i = 0; i < _turnOrder.Count; i++)
        {
            int idx = (start + i) % _turnOrder.Count;
            if (_hp[_turnOrder[idx].Id] > 0) return idx;
        }
        return 0;
    }

    private BattleEvent AddEvent(string actorId, string description, string type, string? targetId = null, int value = 0)
    {
        var ev = new BattleEvent(actorId, description, type, targetId, value);
        _log.Add(ev);
        return ev;
    }
}
