using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Unit tests for the modifier action group ordering: Set → Modify → Add.
    /// Uses an in-memory content source to isolate behaviour from the GameData files.
    /// </summary>
    public class ModifierApplicationTests
    {
        // ── In-memory content source ─────────────────────────────────────────

        /// <summary>Minimal in-memory <see cref="IContentSource"/> for pipeline tests.</summary>
        private sealed class InMemoryContentSource : IContentSource
        {
            private readonly Dictionary<string, string> _files;

            public InMemoryContentSource(Dictionary<string, string> files) => _files = files;

            public string ReadAllText(string relativePath) => _files[relativePath];
            public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);
            public bool DirectoryExists(string relativeDirectory)
            {
                var prefix = relativeDirectory.TrimEnd('/') + "/";
                return _files.Keys.Any(k => k.StartsWith(prefix));
            }
            public IEnumerable<string> ListFiles(string relativeDirectory, string searchPattern)
            {
                var prefix = relativeDirectory.TrimEnd('/') + "/";
                return _files.Keys.Where(k => k.StartsWith(prefix)).ToArray();
            }
        }

        // ── Shared skill YAML ─────────────────────────────────────────────────

        private const string SkillYaml = @"
- id: sword-strike
  name: Sword Strike
  cost: 3
  damageMultiplier: 1.5
  cooldown: 2
  initialCooldown: 1
  effects:
    - kind: Damage
      target: Enemy
      damagePerHit:
        - damageType: physical
          scaling:
            - stat: str
              scale: 1.0
";

        private const string UnitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
skills:
  - id: sword-strike
    modifiers:
      - {0}
";

        // ── Set action ────────────────────────────────────────────────────────

        [Fact]
        public void Set_OverridesCost_ToNewValue()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    cost: 0
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(0, skill.Cost);
        }

        [Fact]
        public void Set_OverridesDamageMultiplier()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    damageMultiplier: 2.5
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(2.5, skill.DamageMultiplier);
        }

        [Fact]
        public void Set_OverridesIsAoe()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    isAoe: true
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.True(skill.IsAoe);
        }

        [Fact]
        public void Set_OverridesCooldown()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    cooldown: 0
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(0, skill.Cooldown);
        }

        [Fact]
        public void Set_OverridesInitialCooldown()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    initialCooldown: 0
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(0, skill.InitialCooldown);
        }

        [Fact]
        public void Set_LastModifierWins_WhenMultipleModsSetSameKey()
        {
            // Two mods both set cost — last one (cost: 1) should win.
            const string unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
skills:
  - id: sword-strike
    modifiers:
      - set-cost-zero
      - set-cost-one
";
            var db = BuildDb(@"
- id: set-cost-zero
  set:
    cost: 0
- id: set-cost-one
  set:
    cost: 1
", unitYaml);

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(1, skill.Cost);
        }

        // ── Modify action ─────────────────────────────────────────────────────

        [Fact]
        public void Modify_AddsDeltaToCost()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    cost: -1
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(2, skill.Cost); // base 3 + delta -1
        }

        [Fact]
        public void Modify_AddsDeltaToCooldown()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    cooldown: 1
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(3, skill.Cooldown); // base 2 + delta 1
        }

        [Fact]
        public void Modify_SumsAcrossMultipleMods()
        {
            const string unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
skills:
  - id: sword-strike
    modifiers:
      - mod-a
      - mod-b
";
            var db = BuildDb(@"
- id: mod-a
  modify:
    cost: -1
- id: mod-b
  modify:
    cost: -1
", unitYaml);

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(1, skill.Cost); // base 3 + (-1) + (-1)
        }

        // ── Set then Modify ordering ──────────────────────────────────────────

        [Fact]
        public void SetThenModify_ModifyAppliedAfterSet()
        {
            // Set cost to 0, then add delta +2 — result should be 0 + 2 = 2, not 3 + 2 = 5.
            const string unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
skills:
  - id: sword-strike
    modifiers:
      - set-cost-zero
      - add-cost-two
";
            var db = BuildDb(@"
- id: set-cost-zero
  set:
    cost: 0
- id: add-cost-two
  modify:
    cost: 2
", unitYaml);

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(2, skill.Cost);
        }

        // ── Add action ────────────────────────────────────────────────────────

        [Fact]
        public void Add_InjectsDamageComponentIntoFirstEffect()
        {
            var db = BuildDb(@"
- id: test-mod
  add:
    damagePerHit:
      - damageType: fire
        scaling:
          - stat: wis
            scale: 0.5
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            var components = skill.Effects[0].DamagePerHit;
            Assert.Equal(2, components.Count); // original physical + injected fire
            Assert.Contains(components, c => c.DamageType == EffectType.Fire);
        }

        [Fact]
        public void Add_AppendedAfterOriginalComponents()
        {
            var db = BuildDb(@"
- id: test-mod
  add:
    damagePerHit:
      - damageType: fire
        scaling:
          - stat: wis
            scale: 0.5
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            var components = skill.Effects[0].DamagePerHit;
            // Original physical component must still be first.
            Assert.Equal(EffectType.Physical, components[0].DamageType);
            Assert.Equal(EffectType.Fire, components[1].DamageType);
        }

        [Fact]
        public void Add_ScalingIsPreservedOnInjectedComponent()
        {
            var db = BuildDb(@"
- id: test-mod
  add:
    damagePerHit:
      - damageType: fire
        scaling:
          - stat: wis
            scale: 0.5
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            var fire = skill.Effects[0].DamagePerHit.First(c => c.DamageType == EffectType.Fire);
            Assert.Single(fire.Scaling);
            Assert.Equal("wis", fire.Scaling[0].Stat);
            Assert.Equal(0.5, fire.Scaling[0].Scale);
        }

        // ── Full Set + Modify + Add ordering ─────────────────────────────────

        [Fact]
        public void AllThreeGroups_AppliedInCorrectOrder()
        {
            // set cost to 0, modify cooldown -1, add fire component.
            var db = BuildDb(@"
- id: test-mod
  set:
    cost: 0
  modify:
    cooldown: -1
  add:
    damagePerHit:
      - damageType: fire
        scaling:
          - stat: wis
            scale: 0.5
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(0, skill.Cost);
            Assert.Equal(1, skill.Cooldown); // base 2 + (-1)
            Assert.Equal(2, skill.Effects[0].DamagePerHit.Count);
            Assert.Contains(skill.Effects[0].DamagePerHit, c => c.DamageType == EffectType.Fire);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ContentDatabase BuildDb(string modifiersYaml, string unitYaml)
        {
            var files = new Dictionary<string, string>
            {
                ["modifiers.yml"] = modifiersYaml,
                ["skills/sword-strike.yml"] = SkillYaml,
                ["units/test-unit.yml"] = unitYaml,
            };
            return ContentPipeline.Load(new InMemoryContentSource(files));
        }

        private static BattleSkill GetSkill(ContentDatabase db, string unitId, string skillId)
        {
            var unit = db.GetUnit(unitId);
            return unit.ResolvedSkills.First(s => s.Id == skillId);
        }
    }
}
