namespace GameCore.Battle;

/// <summary>
/// Immutable definition of a unit entering a battle.
/// </summary>
public record BattleUnit(
    string Id,
    string Name,
    string Team,
    int Level,
    int Str,
    int Wis,
    int Agi,
    int MaxMp = 0,
    IReadOnlyList<BattleSkill>? Skills = null
)
{
    // ── Derived stats ────────────────────────────────────────────────
    /// <summary>Max HP derived from STR.</summary>
    public int MaxHp => Str * 100;
    /// <summary>Physical damage derived from STR.</summary>
    public int PhysAttack => Str * 8;
    /// <summary>Magic damage derived from WIS.</summary>
    public int MagicAttack => Wis * 8;
    /// <summary>Effective attack power — highest of physical or magic.</summary>
    public int Attack => Math.Max(PhysAttack, MagicAttack);
    /// <summary>Turn order priority derived from AGI.</summary>
    public int Initiative => Agi;
    /// <summary>Hits per action: 1 base + 1 per 100 AGI.</summary>
    public int HitCount => 1 + Agi / 100;

    /// <summary>
    /// The unit's skill list. Always has at least one skill (the free basic action).
    /// If none were provided, a default "Attack" skill is used.
    /// </summary>
    public IReadOnlyList<BattleSkill> ResolvedSkills => Skills is { Count: > 0 }
        ? Skills
        : [new BattleSkill("attack", "Attack", MpCost: 0, Multiplier: 1.0)];
}
