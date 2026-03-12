namespace GameCore.Scenarios;

/// <summary>
/// An <see cref="IBattleScenario"/> that also declares the exact expected outcome of its battle.
/// <para>
/// Every registered <see cref="IRegressionScenario"/> is validated automatically by the test
/// suite — no additional test code is required. To capture a new regression case:
/// implement this interface on the scenario, fill in the expected values from a known-good run,
/// and add it to <see cref="ScenarioRegistry"/>.
/// </para>
/// </summary>
public interface IRegressionScenario : IBattleScenario
{
    /// <summary>The team expected to win: "player" or "enemy".</summary>
    string ExpectedWinner { get; }

    /// <summary>
    /// The exact number of snapshots expected in <see cref="GameCore.Battle.BattleResult.Snapshots"/>.
    /// Locks in the full battle length — a change here signals a non-trivial engine or scenario change.
    /// </summary>
    int ExpectedSnapshotCount { get; }
}
