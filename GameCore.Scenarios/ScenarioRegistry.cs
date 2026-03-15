using System.Collections.Generic;
using System.Linq;

namespace GameCore.Scenarios
{
    public static class ScenarioRegistry
    {
        private static readonly IBattleScenario[] _all = new IBattleScenario[]
        {
            new SampleScenario(),
            new WeaponArchetypeScenario(),
            new SpellArchetypeScenario(),
            new AllArchetypesScenario(),
        };

        // All registered scenarios, in display order.
        public static IReadOnlyList<IBattleScenario> All => _all;

        // Scenarios with declared expected outcomes; validated automatically by the test suite.
        public static IEnumerable<IRegressionScenario> RegressionScenarios =>
            _all.OfType<IRegressionScenario>();
    }
}
