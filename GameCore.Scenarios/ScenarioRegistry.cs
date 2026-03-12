namespace GameCore.Scenarios
{
    /// <summary>
    /// The single source of truth for all registered battle scenarios.
    /// Tests and the battle sandbox both consume this list — setup logic is never duplicated.
    /// <para>
    /// To add a new scenario: create the class, add it here.
    /// It will automatically appear in the sandbox and be picked up by all structural and
    /// regression tests — no additional test code required.
    /// </para>
    /// </summary>
    public static class ScenarioRegistry
    {
        private static readonly IBattleScenario[] _all =
        [
            new SampleScenario(),
            new WatchScenario(new SampleScenario()),
        ];

        /// <summary>All registered scenarios, in display order.</summary>
        public static IReadOnlyList<IBattleScenario> All => _all;

        /// <summary>
        /// All registered scenarios that declare expected outcomes.
        /// The test suite validates every entry here automatically.
        /// </summary>
        public static IEnumerable<IRegressionScenario> RegressionScenarios =>
            _all.OfType<IRegressionScenario>();
    }
}
