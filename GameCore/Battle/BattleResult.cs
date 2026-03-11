namespace GameCore.Battle;

/// <summary>
/// The full result of a completed battle: all snapshots and the winning team.
/// </summary>
public class BattleResult
{
    public required IReadOnlyList<BattleSnapshot> Snapshots { get; init; }
    public required string WinningTeam { get; init; }
    public required int Seed { get; init; }
}
