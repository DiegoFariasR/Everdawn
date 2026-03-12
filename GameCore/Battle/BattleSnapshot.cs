namespace GameCore.Battle;

/// <summary>
/// The state of a single unit captured in a snapshot.
/// HP is tracked separately; all secondary bars (MP, Focus, Fury, …) live in <see cref="Bars"/>.
/// </summary>
public record UnitState(string UnitId, int CurrentHp, bool IsAlive, IReadOnlyDictionary<string, int>? Bars = null)
{
    /// <summary>Returns the current value of a named bar, or 0 if the unit does not have it.</summary>
    public int GetBar(string key) => Bars != null && Bars.TryGetValue(key, out int v) ? v : 0;
}

/// <summary>
/// State of all units at a given step in the battle, paired with the event that produced it.
/// </summary>
public class BattleSnapshot
{
    public required int Step { get; init; }
    public required BattleEvent Event { get; init; }
    public required IReadOnlyList<UnitState> UnitStates { get; init; }
}
