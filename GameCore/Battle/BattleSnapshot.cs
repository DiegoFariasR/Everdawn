namespace GameCore.Battle;

/// <summary>
/// The state of a single unit captured in a snapshot.
/// </summary>
public record UnitState(string UnitId, int CurrentHp, int CurrentMp, bool IsAlive, int CurrentFocus = 0);

/// <summary>
/// State of all units at a given step in the battle, paired with the event that produced it.
/// </summary>
public class BattleSnapshot
{
    public required int Step { get; init; }
    public required BattleEvent Event { get; init; }
    public required IReadOnlyList<UnitState> UnitStates { get; init; }
}
