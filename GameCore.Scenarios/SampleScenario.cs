using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>
    /// A sample battle: three heroes vs four enemies, mirroring the reference battle image.
    /// Seed 42 is fixed — same inputs always produce the same battle.
    /// <para>Also implements <see cref="IRegressionScenario"/>: expected outcomes are declared here
    /// and validated automatically by the test suite.</para>
    /// </summary>
    public class SampleScenario : IBattleScenario, IRegressionScenario
    {
        public string Id => "sample-scenario";
        public string DisplayName => "Sample Battle";
        public int Seed => 42;
        public bool IsPlayable => true;

        // Values established from a known-good run using BattleSession.RunFull() as the
        // single authoritative execution path. Update intentionally if the engine or
        // scenario changes in a way that is expected and reviewed.
        public string ExpectedWinner => "player";
        // 1 start + round banners + skill/attack/death/heal events + 1 end = 60
        public int ExpectedSnapshotCount => 71;

        public override string ToString() => $"{DisplayName} [{Id}]";

        private static readonly string[] PlayerUnitIds = { "paladin", "mage", "rogue" };
        private static readonly string[] EnemyUnitIds = { "goblin-w", "goblin-a", "necro" };

        public BattleSetup CreateSetup(IContentSource source)
        {
            var db = ContentPipeline.Load(source);
            return new BattleSetup
            {
                PlayerUnits = db.GetUnits(PlayerUnitIds)
                                .Select(u => u with { Team = "player" }).ToList(),
                EnemyUnits = db.GetUnits(EnemyUnitIds)
                               .Select(u => u with { Team = "enemy" }).ToList(),
            };
        }
    }
}
