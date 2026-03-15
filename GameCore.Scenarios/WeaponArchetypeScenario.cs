using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>
    /// A scenario showcasing the four weapon archetype attacker patterns:
    /// pure-STR (Mace), STR/AGI (Sword), pure-AGI (Bow), AGI-multihit (Dagger)
    /// vs a standard enemy party.
    /// Useful for observing how weapon archetypes behave and comparing their output.
    /// </summary>
    public sealed class WeaponArchetypeScenario : IBattleScenario
    {
        public string Id => "weapon-archetypes";
        public string DisplayName => "Weapon Archetypes";
        public int Seed => 7;
        public bool IsPlayable => true;

        private static readonly string[] EnemyUnitIds = { "goblin-w", "goblin-a", "necro" };

        public BattleSetup CreateSetup(IContentSource source)
        {
            var db = ContentPipeline.Load(source);
            return new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    // Pure STR: high damage per hit, highest HP.
                    new BattleUnit("arch-warrior", "Warrior",  "player", Level: 10, Str: 100, Wis: 5, Agi: 5,   Skills: new[] { db.GetSkill("mace-strike") },   EquipmentType: EquipmentType.Blunt),
                    // STR + AGI: moderate damage, extra hits from AGI.
                    new BattleUnit("arch-knight",  "Knight",   "player", Level: 10, Str: 70,  Wis: 5, Agi: 30,  Skills: new[] { db.GetSkill("sword-strike") },  EquipmentType: EquipmentType.Slash),
                    // Pure AGI: fixed multi-hit (1.8×), damage scales with AGI. Low HP.
                    new BattleUnit("arch-archer",  "Archer",   "player", Level: 10, Str: 30,  Wis: 5, Agi: 100, Skills: new[] { db.GetSkill("bow-shot") },      EquipmentType: EquipmentType.Bow),
                    // AGI multihit: hits scale with AGI (1 per 100), half-AGI per hit.
                    new BattleUnit("arch-rogue",   "Rogue",    "player", Level: 10, Str: 30,  Wis: 5, Agi: 100, Skills: new[] { db.GetSkill("dagger-strike") }, EquipmentType: EquipmentType.Pierce),
                },
                EnemyUnits = db.GetUnits(EnemyUnitIds)
                               .Select(u => u with { Team = "enemy" }).ToList(),
            };
        }

        public override string ToString() => $"{DisplayName} [{Id}]";
    }
}
