namespace GameCore.Battle;

/// <summary>
/// A stateful interactive battle session. Exposes a single entry point:
/// <see cref="HandleRequest"/>. Clients send <see cref="BattleRequest"/> messages
/// and receive a <see cref="BattleResponse"/> — no direct session properties are
/// ever queried. This mirrors how the UnityClient would talk to GameCore.
/// </summary>
public class InteractiveBattleSession
{
    private readonly List<BattleUnit> _allUnits;
    private readonly List<BattleUnit> _turnOrder;
    private readonly Dictionary<string, int> _hp;
    private readonly Dictionary<string, int> _mp;
    private readonly Random _rng;
    private readonly List<BattleEvent> _log = [];

    private int _turnIndex;
    private int _round = 1;
    private bool _started;
    private readonly Dictionary<string, int> _skillCooldowns = new();
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
        AutoPlayerActionRequest   r => HandleAutoPlayerAction(r),
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
        var skill  = actor.ResolvedSkills.FirstOrDefault(s => s.Id == r.SkillId)
                     ?? throw new ArgumentException($"Unknown skill: {r.SkillId}");

        List<BattleUnit> targets;
        if (skill.IsAoe)
        {
            targets = _allUnits.Where(u => TargetSideMatch(u, actor, skill.Target) && _hp[u.Id] > 0).ToList();
        }
        else
        {
            var t = _allUnits.FirstOrDefault(u => u.Id == r.TargetId)
                    ?? throw new ArgumentException($"Unknown target: {r.TargetId}");
            targets = [t];
        }

        var newEvents = new List<BattleEvent>(ExecuteAction(actor, targets, skill));
        if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
        newEvents.AddRange(AutoAdvance());
        return BuildResponse(newEvents);
    }

    private BattleResponse HandleAutoPlayerAction(AutoPlayerActionRequest _)
    {
        if (!_started) throw new InvalidOperationException("Session not started.");
        if (_isOver)   throw new InvalidOperationException("Battle is over.");
        if (_turnOrder[_turnIndex].Team != "player")
            throw new InvalidOperationException("Not a player turn.");

        var actor   = _turnOrder[_turnIndex];
        var skill   = actor.ResolvedSkills.Where(s => _mp[actor.Id] >= s.MpCost && _skillCooldowns.GetValueOrDefault(s.Id) <= 0).Last();
        var targets = ResolveAutoTargets(actor, skill);
        if (targets.Count == 0) { CheckEnd(); return BuildResponse([]); }

        var newEvents = new List<BattleEvent>(ExecuteAction(actor, targets, skill));
        if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
        newEvents.AddRange(AutoAdvance());
        return BuildResponse(newEvents);
    }

    // ── Response builder ─────────────────────────────────────────────────

    private BattleResponse BuildResponse(IReadOnlyList<BattleEvent> newEvents)
    {
        BattlePendingInput? pending = null;
        if (!_isOver && _turnOrder[_turnIndex].Team == "player")
        {
            var actor  = _turnOrder[_turnIndex];
            var skills = actor.ResolvedSkills;
            pending = new BattlePendingInput(
                Actor:             actor,
                Skills:            skills,
                AvailableSkillIds: skills.Where(s => _mp[actor.Id] >= s.MpCost && _skillCooldowns.GetValueOrDefault(s.Id) <= 0).Select(s => s.Id).ToArray(),
                EnemyTargets:      _allUnits.Where(u => u.Team != actor.Team && _hp[u.Id] > 0).ToArray(),
                AllyTargets:       _allUnits.Where(u => u.Team == actor.Team && _hp[u.Id] > 0).ToArray(),
                SkillCooldowns:    skills.Where(s => _skillCooldowns.GetValueOrDefault(s.Id) > 0)
                                         .ToDictionary(s => s.Id, s => _skillCooldowns[s.Id])
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
            var skill   = actor.ResolvedSkills.Where(s => _mp[actor.Id] >= s.MpCost && _skillCooldowns.GetValueOrDefault(s.Id) <= 0).Last();
            var targets = ResolveAutoTargets(actor, skill);
            if (targets.Count == 0) { CheckEnd(); break; }

            produced.AddRange(ExecuteAction(actor, targets, skill));
            if (AdvanceTurn()) produced.AddRange(StartOfRound());
        }
        return produced;
    }

    /// <summary>Resolves the target list for auto actions (AI and auto mode).</summary>
    private List<BattleUnit> ResolveAutoTargets(BattleUnit actor, BattleSkill skill)
    {
        if (skill.Target == BattleSkillTarget.Ally)
        {
            var allies = _allUnits.Where(u => u.Team == actor.Team && _hp[u.Id] > 0).ToList();
            if (allies.Count == 0) return [];
            // Heal the lowest HP ally
            var target = skill.IsAoe ? null : allies.MinBy(u => _hp[u.Id]);
            return skill.IsAoe ? allies : [target!];
        }
        else
        {
            var foes = _allUnits.Where(u => u.Team != actor.Team && _hp[u.Id] > 0).ToList();
            if (foes.Count == 0) return [];
            return skill.IsAoe ? foes : [foes[_rng.Next(foes.Count)]];
        }
    }

    private static bool TargetSideMatch(BattleUnit u, BattleUnit actor, BattleSkillTarget side) =>
        side == BattleSkillTarget.Ally
            ? u.Team == actor.Team
            : u.Team != actor.Team;

    private IReadOnlyList<BattleEvent> ExecuteAction(BattleUnit actor, List<BattleUnit> targets, BattleSkill skill)
    {
        var produced = new List<BattleEvent>();

        // Tick down this actor's cooldowns (one turn has passed for them)
        foreach (var s in actor.ResolvedSkills)
            if (_skillCooldowns.TryGetValue(s.Id, out int cd) && cd > 0)
                _skillCooldowns[s.Id] = cd - 1;

        // Consume MP once (skill[0] always has MpCost == 0)
        _mp[actor.Id] = Math.Max(0, _mp[actor.Id] - skill.MpCost);

        // Determine event type from skill index for CSS colouring
        var skills   = actor.ResolvedSkills;
        int skillIdx = skills.ToList().IndexOf(skill);
        if (skillIdx < 0) skillIdx = 0;
        string evType = skill.IsHeal ? "skill"
                       : skillIdx == 0 ? "attack" : skillIdx == 1 ? "skill" : "soulburn";

        if (skill.IsAoe && targets.Count > 1)
            produced.Add(AddEvent(actor.Id, $"{actor.Name} unleashes {skill.Name} on all enemies!", evType));

        foreach (var target in targets)
        {
            int variance = Math.Max(1, actor.Attack / 5);
            int amount   = (int)(actor.Attack * skill.Multiplier) + _rng.Next(-variance, variance + 1);

            if (skill.IsHeal)
            {
                var maxHp  = _allUnits.First(u => u.Id == target.Id).MaxHp;
                int healed = Math.Min(amount, maxHp - _hp[target.Id]);
                _hp[target.Id] = Math.Min(maxHp, _hp[target.Id] + amount);
                produced.Add(AddEvent(actor.Id,
                    $"{actor.Name} heals {target.Name} for {healed} HP.",
                    evType, target.Id, healed));
            }
            else
            {
                _hp[target.Id] = Math.Max(0, _hp[target.Id] - amount);
                produced.Add(AddEvent(actor.Id,
                    skill.IsAoe
                        ? $"  \u2192 {target.Name} takes {amount} damage."
                        : $"{actor.Name} uses {skill.Name} on {target.Name} for {amount} damage.",
                    evType, target.Id, amount));

                if (_hp[target.Id] <= 0)
                    produced.Add(AddEvent(target.Id, $"{target.Name} is defeated!", "death"));
            }
        }

        // Apply cooldown for the used skill
        if (skill.Cooldown > 0)
            _skillCooldowns[skill.Id] = skill.Cooldown;

        CheckEnd();
        return produced;
    }

    private bool AdvanceTurn()
    {
        if (_isOver) return false;
        int prev = _turnIndex;
        for (int i = 0; i < _turnOrder.Count; i++)
        {
            _turnIndex = (_turnIndex + 1) % _turnOrder.Count;
            if (_hp[_turnOrder[_turnIndex].Id] > 0)
                return _turnIndex <= prev;  // wrapped = new round started
        }
        CheckEnd();
        return false;
    }

    /// <summary>Called when the turn order wraps. Increments round, regenerates mana.</summary>
    private IReadOnlyList<BattleEvent> StartOfRound()
    {
        _round++;
        var events = new List<BattleEvent>();
        events.Add(AddEvent("system", $"\u2500\u2500 Round {_round} \u2500\u2500", "round"));
        foreach (var u in _allUnits.Where(u => u.MaxMp > 0 && _hp[u.Id] > 0))
        {
            int regen  = Math.Max(1, u.MaxMp / 5);  // 20 % of MaxMp
            int gained = Math.Min(regen, u.MaxMp - _mp[u.Id]);
            if (gained > 0)
            {
                _mp[u.Id] += gained;
                events.Add(AddEvent(u.Id, $"{u.Name} recovers {gained} MP.", "round"));
            }
        }
        return events;
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
