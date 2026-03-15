using System.Linq;
using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
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
        // Updated to 125 after adding mage-meditate and rogue-concentrate skills.
        // Updated to 111 after adding mace-shatter (ultimate) to the paladin.
        // Updated to 103 after adding BuildupPower: thermal buildup decoupled from raw damage,
        //   fire spells now use explicit buildupPower: 50 (vs. old WIS-scaled raw damage),
        //   resulting in fewer/different thermal status events.
        // Updated to 94 after lowering mage-bolt buildupPower 50 → 30 (bolt no longer
        //   triggers burning in one hit, fewer DOT events in the sample battle).
        // Updated to 91 after further lowering burst/meteor/necro buildupPower 50 → 20.
        public int ExpectedSnapshotCount => 91;

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
