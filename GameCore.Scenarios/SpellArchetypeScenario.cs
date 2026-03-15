using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios
{
    /// <summary>
    /// A scenario showcasing the four spell archetype attack patterns:
    /// pure-WIS single-hit (Bolt), WIS/AGI multi-hit (Missiles),
    /// WIS fixed-2hit (Burst), WIS fixed-3hit (Volley)
    /// vs a standard enemy party.
    /// Useful for observing how spell archetypes behave and comparing hit-pattern output.
    /// </summary>
    public sealed class SpellArchetypeScenario : IBattleScenario
    {
        public string Id => "spell-archetypes";
        public string DisplayName => "Spell Archetypes";
        public int Seed => 13;
        public bool IsPlayable => true;

        private static readonly string[] EnemyUnitIds = { "goblin-w", "goblin-a", "necro" };

        public BattleSetup CreateSetup(IContentSource source)
        {
            var db = ContentPipeline.Load(source);
            return new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    // Pure WIS, 1 hit: baseline void damage.
                    new BattleUnit("arch-bolt",     "Bolt Mage",     "player", Level: 10, Str: 30, Wis: 100, Agi: 5,  Skills: new[] { db.GetSkill("mage-bolt") },      Traits: new[] { BattleTrait.ManaUser }),
                    // WIS + AGI: 0.5× WIS per hit, hits scale with AGI (1 per 100).
                    new BattleUnit("arch-missiles", "Missile Mage",  "player", Level: 10, Str: 30, Wis: 60,  Agi: 40, Skills: new[] { db.GetSkill("spell-missiles") }, Traits: new[] { BattleTrait.ManaUser }),
                    // WIS, 2 fixed hits: 0.6× WIS each. Same stats as bolt, different burst.
                    new BattleUnit("arch-burst",    "Burst Mage",    "player", Level: 10, Str: 30, Wis: 100, Agi: 5,  Skills: new[] { db.GetSkill("spell-burst") },    Traits: new[] { BattleTrait.ManaUser }),
                    // WIS, 3 fixed hits: 0.4× WIS each. High hit count, lower per-hit.
                    new BattleUnit("arch-volley",   "Volley Mage",   "player", Level: 10, Str: 30, Wis: 100, Agi: 5,  Skills: new[] { db.GetSkill("spell-volley") },   Traits: new[] { BattleTrait.ManaUser }),
                },
                EnemyUnits = db.GetUnits(EnemyUnitIds)
                               .Select(u => u with { Team = "enemy" }).ToList(),
            };
        }

        public override string ToString() => $"{DisplayName} [{Id}]";
    }
}
