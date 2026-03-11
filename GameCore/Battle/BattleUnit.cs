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
    int Initiative
);
