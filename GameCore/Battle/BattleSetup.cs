namespace GameCore.Battle;

/// <summary>
/// Defines both sides entering a battle.
/// </summary>
public class BattleSetup
{
    public required IReadOnlyList<BattleUnit> PlayerUnits { get; init; }
    public required IReadOnlyList<BattleUnit> EnemyUnits { get; init; }
}
