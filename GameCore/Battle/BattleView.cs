namespace GameCore.Battle;

/// <summary>
/// A display-safe view of one skill, exposed to the client via <see cref="PendingInputView"/>.
/// Contains everything needed to render skill buttons and UX targeting hints.
/// Internal damage math remains hidden inside BattleCore.
/// </summary>
public sealed record SkillView(
    string Id,
    string Name,
    int MpCost,
    double Multiplier,
    bool IsAoe,
    bool IsHeal,
    int Cooldown,
    BattleSkillTarget Target,
    EffectType EffectType,
    bool IsBasic,
    bool IsUltimate,
    int EffectiveInitialCooldown,
    /// <summary>Estimated damage = actor base attack × multiplier, for display purposes.</summary>
    int BaseDmg
);

/// <summary>
/// Describes the action input required from the player, when it is a player's turn.
/// Present on <see cref="BattleView.PendingInput"/> only when a player unit needs to act.
/// Null when it is an enemy turn, or when the battle is over.
/// </summary>
public sealed record PendingInputView(
    /// <summary>ID of the unit whose turn it is.</summary>
    string ActorId,
    /// <summary>Display name of the acting unit.</summary>
    string ActorName,
    /// <summary>All skills this unit has. Index 0 is always the free basic action.</summary>
    IReadOnlyList<SkillView> Skills,
    /// <summary>IDs of skills the unit can currently use (passes MP and cooldown checks).</summary>
    IReadOnlyList<string> AvailableSkillIds,
    /// <summary>IDs of living enemy units — valid targets for enemy-targeting skills.</summary>
    IReadOnlyList<string> EnemyTargetIds,
    /// <summary>IDs of living ally units including self — valid targets for ally-targeting skills.</summary>
    IReadOnlyList<string> AllyTargetIds,
    /// <summary>Remaining cooldown turns per skill ID. Only contains entries where cooldown > 0.</summary>
    IReadOnlyDictionary<string, int> SkillCooldowns
);

/// <summary>
/// The authoritative current state of the battle, as seen by the client.
/// Returned by every <see cref="IBattleEngine"/> call. The client renders this — it never
/// holds its own copies of HP, turn state, or victory status.
/// </summary>
public sealed record BattleView(
    /// <summary>Current HP, MP, status effects, and alive status for every unit.</summary>
    IReadOnlyList<UnitState> Units,
    /// <summary>Non-null when it is the player's turn and input is required.</summary>
    PendingInputView? PendingInput,
    /// <summary>Complete event log from battle start.</summary>
    IReadOnlyList<BattleEvent> FullLog,
    bool IsOver,
    string? WinningTeam,
    /// <summary>Current round number (1-based). Increments at the end of each full turn-order cycle.</summary>
    int Round = 1
);
