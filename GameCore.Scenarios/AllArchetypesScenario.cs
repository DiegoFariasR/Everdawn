using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>
    /// All 13 stat archetypes as player units, each equipped with every weapon and spell skill.
    /// Mirrors the archetype table in the balancing report — useful for testing any stat build
    /// with any skill combination interactively.
    /// Budget: 100 stat points per archetype, Level 10.
    /// </summary>
    public sealed class AllArchetypesScenario : IBattleScenario
    {
        public string Id => "all-archetypes";
        public string DisplayName => "All Archetypes";
        public int Seed => 7;
        public bool IsPlayable => true;

        private const int Budget = 100;
        private const int Level = 10;

        private static readonly (string Id, string Name, double Str, double Agi, double Wis)[] Archetypes =
        {
            ("arch-str100",     "STR pure",  1.00,    0.00,    0.00    ),
            ("arch-agi100",     "AGI pure",  0.00,    1.00,    0.00    ),
            ("arch-wis100",     "WIS pure",  0.00,    0.00,    1.00    ),
            ("arch-str50agi50", "STR/AGI",   0.50,    0.50,    0.00    ),
            ("arch-agi50wis50", "AGI/WIS",   0.00,    0.50,    0.50    ),
            ("arch-wis50str50", "WIS/STR",   0.50,    0.00,    0.50    ),
            ("arch-str75agi25", "STR+AGI",   0.75,    0.25,    0.00    ),
            ("arch-agi75wis25", "AGI+WIS",   0.00,    0.75,    0.25    ),
            ("arch-wis75str25", "WIS+STR",   0.25,    0.00,    0.75    ),
            ("arch-str25agi75", "AGI-STR",   0.25,    0.75,    0.00    ),
            ("arch-agi25wis75", "WIS-AGI",   0.00,    0.25,    0.75    ),
            ("arch-wis25str75", "STR-WIS",   0.75,    0.00,    0.25    ),
            ("arch-balanced",   "Balanced",  1.0/3.0, 1.0/3.0, 1.0/3.0),
        };

        private static readonly string[] AllSkillIds =
        {
            "mace-strike", "sword-strike", "bow-shot", "dagger-strike",
            "mage-bolt", "spell-missiles", "spell-burst", "spell-volley",
        };

        private static readonly string[] EnemyUnitIds = { "goblin-w", "goblin-a", "necro" };

        public BattleSetup CreateSetup(IContentSource source)
        {
            var db = ContentPipeline.Load(source);
            var allSkills = AllSkillIds.Select(id => db.GetSkill(id)).ToArray();

            var playerUnits = new List<BattleUnit>();
            foreach (var (id, name, strFrac, agiFrac, wisFrac) in Archetypes)
            {
                playerUnits.Add(new BattleUnit(
                    id, name, "player", Level: Level,
                    Str: Math.Max(1, (int)(Budget * strFrac)),
                    Wis: Math.Max(1, (int)(Budget * wisFrac)),
                    Agi: Math.Max(1, (int)(Budget * agiFrac)),
                    Skills: allSkills,
                    // Grant MagicUser and a Blunt weapon so all benchmark skills are usable.
                    // This scenario is a sandbox for stat-vs-skill damage analysis, not a
                    // lore-accurate setup — requirements are intentionally satisfied broadly.
                    Traits: new[] { BattleTrait.MagicUser },
                    EquipmentType: EquipmentType.Blunt));
            }

            return new BattleSetup
            {
                PlayerUnits = playerUnits,
                EnemyUnits = db.GetUnits(EnemyUnitIds)
                                .Select(u => u with { Team = "enemy" }).ToList(),
            };
        }

        public override string ToString() => $"{DisplayName} [{Id}]";
    }
}
