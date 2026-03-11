namespace GameCore.Battle;

/// <summary>
/// Immutable definition of a unit entering a battle.
/// </summary>
public record BattleUnit(
    string Id,
    string Name,
    string Team,
    int Level,
    int MaxHp,
    int MaxMp,
    int Attack,
    int Initiative,
    IReadOnlyList<BattleSkill>? Skills = null
)
{
    /// <summary>
    /// The unit's skill list. Always has at least one skill (the free basic action).
    /// If none were provided, a default "Attack" skill is used.
    /// </summary>
    public IReadOnlyList<BattleSkill> ResolvedSkills => Skills is { Count: > 0 }
        ? Skills
        : [new BattleSkill("attack", "Attack", MpCost: 0, Multiplier: 1.0)];
}
