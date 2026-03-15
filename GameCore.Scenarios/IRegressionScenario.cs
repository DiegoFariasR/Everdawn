namespace GameCore.Scenarios
{
    /// <summary>An <see cref="IBattleScenario"/> that also declares the exact expected outcome of its battle.</summary>
    public interface IRegressionScenario : IBattleScenario
    {
        /// <summary>The team expected to win: "player" or "enemy".</summary>
        string ExpectedWinner { get; }

        /// <summary>The exact number of snapshots expected in the full battle result. Locks in the full battle length.</summary>
        int ExpectedSnapshotCount { get; }
    }
}
