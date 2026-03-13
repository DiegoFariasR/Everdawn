using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameCore.Battle;
using GameCore.Content;
using GameCore.Tests;
using Xunit;
using Xunit.Abstractions;

namespace GameCore.Tests.Battle
{
    [Trait("Category", "Balancing")]
    public class ArchetypeDamageTests
    {
        private readonly ITestOutputHelper _output;
        public ArchetypeDamageTests(ITestOutputHelper output) => _output = output;

        private static readonly int[] Budgets = { 20, 100, 200, 500 };

        private static readonly (string Id, double Str, double Agi, double Wis)[] Archetypes =
        {
            ("str100",      1.00,    0.00,    0.00   ),
            ("agi100",      0.00,    1.00,    0.00   ),
            ("wis100",      0.00,    0.00,    1.00   ),
            ("str50-agi50", 0.50,    0.50,    0.00   ),
            ("agi50-wis50", 0.00,    0.50,    0.50   ),
            ("wis50-str50", 0.50,    0.00,    0.50   ),
            ("str75-agi25", 0.75,    0.25,    0.00   ),
            ("agi75-wis25", 0.00,    0.75,    0.25   ),
            ("wis75-str25", 0.25,    0.00,    0.75   ),
            ("str25-agi75", 0.25,    0.75,    0.00   ),
            ("agi25-wis75", 0.00,    0.25,    0.75   ),
            ("wis25-str75", 0.75,    0.00,    0.25   ),
            ("balanced",    1.0/3.0, 1.0/3.0, 1.0/3.0),
        };

        private static readonly string[] WeaponSkillIds = { "mace-strike", "sword-strike", "bow-shot", "dagger-strike" };
        private static readonly string[] SpellSkillIds = { "mage-bolt", "spell-missiles", "spell-burst", "spell-volley" };

        // Space reserved: add modifier IDs here to sweep them in future runs.
        // private static readonly string[] WeaponModifierIds = { "enchant-fire", "enchant-holy" };

        private static readonly BattleSkill TauntSkill = new BattleSkill(
            "boss-taunt", "Taunt", Cost: 0, DamageMultiplier: 1.0,
            Effects: Array.Empty<SkillEffect>());

        private static BattleUnit BuildBoss(int budget) => new BattleUnit(
            "boss", "Dummy Boss", "enemy", Level: 1,
            Str: 2 * budget, Wis: 0, Agi: 0,
            Skills: new[] { TauntSkill });

        private static BattleUnit BuildAttacker(
            string archetypeId, double strFrac, double agiFrac, double wisFrac,
            int budget, BattleSkill skill) =>
            new BattleUnit(
                $"attacker-{archetypeId}", "Attacker", "player", Level: 1,
                Str: Math.Max(1, (int)(budget * strFrac)),
                Wis: (int)(budget * wisFrac),
                Agi: (int)(budget * agiFrac),
                Skills: new[] { skill });

        public static IEnumerable<object[]> AllCombinations()
        {
            var allSkillIds = WeaponSkillIds.Concat(SpellSkillIds).ToArray();
            foreach (int budget in Budgets)
                foreach (var (id, strF, agiF, wisF) in Archetypes)
                    foreach (var skillId in allSkillIds)
                        yield return new object[] { id, strF, agiF, wisF, budget, skillId };
        }

        public static IEnumerable<object[]> BudgetData() =>
            Budgets.Select(b => new object[] { b });

        [Theory, MemberData(nameof(AllCombinations))]
        public void Engine_AllCombinations_PlayerWins(
            string archetypeId, double strFrac, double agiFrac, double wisFrac,
            int budget, string skillId)
        {
            var db = ContentPipeline.Load(TestContentSource.Default);
            var skill = db.GetSkill(skillId);
            var attacker = BuildAttacker(archetypeId, strFrac, agiFrac, wisFrac, budget, skill);
            var boss = BuildBoss(budget);
            var setup = new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { attacker },
                EnemyUnits = new List<BattleUnit> { boss },
            };
            var result = BattleEngine.Run(setup, seed: 42, maxRounds: 999);
            Assert.Equal("player", result.WinningTeam);
        }

        [Theory, MemberData(nameof(BudgetData))]
        public void BalanceReport_ByBudget(int budget)
        {
            var db = ContentPipeline.Load(TestContentSource.Default);
            var allSkills = WeaponSkillIds.Concat(SpellSkillIds)
                                          .Select(id => db.GetSkill(id))
                                          .ToArray();
            int bossHp = 200 * budget;

            var entries = new List<(string Archetype, string Skill, string Group, int Rounds)>();
            foreach (var (id, strF, agiF, wisF) in Archetypes)
            {
                foreach (var skill in allSkills)
                {
                    var attacker = BuildAttacker(id, strF, agiF, wisF, budget, skill);
                    var boss = BuildBoss(budget);
                    var setup = new BattleSetup
                    {
                        PlayerUnits = new List<BattleUnit> { attacker },
                        EnemyUnits = new List<BattleUnit> { boss },
                    };
                    var result = BattleEngine.Run(setup, seed: 42, maxRounds: 999);
                    int rounds = result.Snapshots.Count(s => s.Event.Type == "round");
                    string group = WeaponSkillIds.Contains(skill.Id) ? "Weapon" : "Spell";
                    entries.Add((id, skill.Id, group, rounds));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"\n=== Balance Report � Budget {budget}  (Boss HP {bossHp}) ===");

            const int colW = 7;
            string hdr = $"{"Archetype",-16}";
            foreach (var s in allSkills) hdr += $"  {(s.Name.Length > colW ? s.Name[..colW] : s.Name),colW}";
            sb.AppendLine();
            sb.AppendLine("ROUNDS-TO-KILL  (lower = faster kill)");
            sb.AppendLine(hdr);
            sb.AppendLine(new string('-', hdr.Length));

            foreach (var (id, _, _, _) in Archetypes)
            {
                var row = new StringBuilder($"{id,-16}");
                foreach (var skill in allSkills)
                {
                    int r = entries.First(e => e.Archetype == id && e.Skill == skill.Id).Rounds;
                    row.Append($"  {r,colW}");
                }
                sb.AppendLine(row.ToString());
            }

            sb.AppendLine();
            sb.AppendLine("PER-SKILL TOP ARCHETYPES");
            foreach (var skill in allSkills)
            {
                var ranked = entries
                    .Where(e => e.Skill == skill.Id)
                    .OrderBy(e => e.Rounds)
                    .ToList();
                int best = ranked.First().Rounds;
                int worst = ranked.Last().Rounds;
                sb.Append($"  {(skill.Name.Length > 12 ? skill.Name[..12] : skill.Name),-12}: ");
                sb.Append(string.Join(", ", ranked.Take(3).Select(e => $"{e.Archetype}({e.Rounds}r)")));
                sb.Append($"  ...  worst: {ranked.Last().Archetype}({worst}r)");
                sb.Append($"  spread: {worst - best}r");
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("PER-ARCHETYPE BEST SKILLS");
            foreach (var (id, _, _, _) in Archetypes)
            {
                var ranked = entries
                    .Where(e => e.Archetype == id)
                    .OrderBy(e => e.Rounds)
                    .ToList();
                sb.Append($"  {id,-16}: ");
                sb.AppendLine(string.Join("  >  ", ranked.Take(3).Select(e => { var sk = allSkills.First(s => s.Id == e.Skill); return $"{(sk.Name.Length > 7 ? sk.Name[..7] : sk.Name)}({e.Rounds}r)"; })));
            }

            int maceRounds = entries.First(e => e.Archetype == "str100" && e.Skill == "mace-strike").Rounds;
            sb.AppendLine();
            sb.AppendLine($"Calibration: str100+mace = {maceRounds} rounds (expect 26)");

            _output.WriteLine(sb.ToString());
            Assert.Equal(26, maceRounds);
        }

    }
}
