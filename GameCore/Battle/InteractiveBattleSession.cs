namespace GameCore.Battle;

/// <summary>
/// A stateful interactive battle session. Player units wait for input;
/// enemy units resolve automatically. Call SubmitAction() on each player turn.
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

    public bool IsOver { get; private set; }
    public string? WinningTeam { get; private set; }

    /// <summary>The unit currently acting. Null when the battle is over.</summary>
    public BattleUnit? CurrentUnit => IsOver ? null : _turnOrder[_turnIndex];

    /// <summary>True when the current unit is a player unit and an action is required.</summary>
    public bool IsPlayerTurn => !IsOver && CurrentUnit!.Team == "player";

    public IReadOnlyList<BattleEvent> Log => _log;

    public IReadOnlyList<UnitState> CurrentState =>
        _allUnits.Select(u => new UnitState(u.Id, _hp[u.Id], _mp[u.Id], _hp[u.Id] > 0)).ToArray();

    public bool CanUseSkill    => CurrentUnit is not null && _mp[CurrentUnit.Id] >= SkillMpCost;
    public bool CanUseSoulBurn => CurrentUnit is not null && _mp[CurrentUnit.Id] >= SoulBurnMpCost;

    public IReadOnlyList<BattleUnit> ValidTargets =>
        IsOver ? [] : _allUnits.Where(u => u.Team != CurrentUnit!.Team && _hp[u.Id] > 0).ToArray();

    public InteractiveBattleSession(BattleSetup setup, int seed)
    {
        _allUnits  = [.. setup.PlayerUnits, .. setup.EnemyUnits];
        _turnOrder = [.. _allUnits.OrderByDescending(u => u.Initiative)];
        _hp        = _allUnits.ToDictionary(u => u.Id, u => u.MaxHp);
        _mp        = _allUnits.ToDictionary(u => u.Id, u => u.MaxMp);
        _rng       = new Random(seed);
    }

    /// <summary>
    /// Creates a session starting from a mid-battle snapshot.
    /// HP/MP are taken from <paramref name="initialState"/>; turn order picks up
    /// after the unit identified by <paramref name="lastActorId"/>.
    /// </summary>
    public InteractiveBattleSession(BattleSetup setup, int seed, IReadOnlyList<UnitState> initialState, string? lastActorId = null)
    {
        _allUnits  = [.. setup.PlayerUnits, .. setup.EnemyUnits];
        _turnOrder = [.. _allUnits.OrderByDescending(u => u.Initiative)];
        _hp        = _allUnits.ToDictionary(u => u.Id,
            u => initialState.FirstOrDefault(s => s.UnitId == u.Id)?.CurrentHp ?? u.MaxHp);
        _mp        = _allUnits.ToDictionary(u => u.Id,
            u => initialState.FirstOrDefault(s => s.UnitId == u.Id)?.CurrentMp ?? u.MaxMp);
        _rng       = new Random(seed);
        _turnIndex = NextAliveIndexAfter(lastActorId);
    }

    // Finds the first alive unit in initiative order starting after the given actor.
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

    /// <summary>
    /// Starts the session. Records the "Battle begins!" event and, if the
    /// first unit is an enemy, auto-resolves until it is the player's turn.
    /// Returns the events produced during startup.
    /// </summary>
    public IReadOnlyList<BattleEvent> Start()
    {
        if (_started) throw new InvalidOperationException("Already started.");
        _started = true;

        AddEvent("system", "Battle begins!", "start");
        return AutoAdvance();
    }

    /// <summary>
    /// Resumes from a snapshot state. Adds a marker event then auto-resolves
    /// any leading enemy turns until the first player turn (or battle end).
    /// </summary>
    public IReadOnlyList<BattleEvent> StartFromSnapshot(int step)
    {
        if (_started) throw new InvalidOperationException("Already started.");
        _started = true;

        CheckEnd();
        if (IsOver)
        {
            AddEvent("system", "The battle was already over at this point.", "takeover");
            return _log;
        }

        AddEvent("system", $"You took control at step {step}.", "takeover");
        return AutoAdvance();
    }

    /// <summary>
    /// Submit a player action. Returns all events from this action plus any
    /// subsequent auto-resolved enemy turns, until the next player turn or
    /// battle end.
    /// </summary>
    public IReadOnlyList<BattleEvent> SubmitAction(PlayerActionType action, string targetId)
    {
        if (IsOver)            throw new InvalidOperationException("Battle is over.");
        if (!IsPlayerTurn)     throw new InvalidOperationException("Not a player turn.");

        var actor  = CurrentUnit!;
        var target = _allUnits.FirstOrDefault(u => u.Id == targetId)
                     ?? throw new ArgumentException($"Unknown target: {targetId}");

        var newEvents = new List<BattleEvent>();
        newEvents.AddRange(ExecuteAction(actor, target, action));
        AdvanceTurn();
        newEvents.AddRange(AutoAdvance());
        return newEvents;
    }

    // ── Internals ────────────────────────────────────────────────────────

    /// <summary>Auto-resolves enemy turns until it's a player's turn or the battle ends.</summary>
    private IReadOnlyList<BattleEvent> AutoAdvance()
    {
        var produced = new List<BattleEvent>();
        while (!IsOver && !IsPlayerTurn)
        {
            var actor   = CurrentUnit!;
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
        if (IsOver) return;
        // Skip dead units
        for (int i = 0; i < _turnOrder.Count; i++)
        {
            _turnIndex = (_turnIndex + 1) % _turnOrder.Count;
            if (_hp[_turnOrder[_turnIndex].Id] > 0) return;
        }
        CheckEnd();
    }

    private void CheckEnd()
    {
        if (IsOver) return;
        bool playerAlive = _allUnits.Any(u => u.Team == "player" && _hp[u.Id] > 0);
        bool enemyAlive  = _allUnits.Any(u => u.Team == "enemy"  && _hp[u.Id] > 0);
        if (!playerAlive || !enemyAlive)
        {
            IsOver      = true;
            WinningTeam = playerAlive ? "player" : "enemy";
            AddEvent("system", playerAlive ? "Victory!" : "Defeat...", "end");
        }
    }

    private BattleEvent AddEvent(string actorId, string description, string type, string? targetId = null, int value = 0)
    {
        var ev = new BattleEvent(actorId, description, type, targetId, value);
        _log.Add(ev);
        return ev;
    }
}
