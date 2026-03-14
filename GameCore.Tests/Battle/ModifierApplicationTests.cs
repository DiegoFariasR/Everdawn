using System;
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
        public void Set_OverridesBaseHits()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    extraHits: 3.0
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(3.0, skill.BaseHits);
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
        public void Modify_AddsExtraHits()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    extraHits: 1
", string.Format(UnitYaml, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(2.0, skill.BaseHits); // base 1.0 + delta 1
        }

        [Fact]
        public void Modify_AddsExtraHits_StacksAcrossMultipleMods()
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
      - add-hit-a
      - add-hit-b
";
            var db = BuildDb(@"
- id: add-hit-a
  modify:
    extraHits: 1
- id: add-hit-b
  modify:
    extraHits: 1
", unitYaml);

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(3.0, skill.BaseHits); // base 1.0 + 1 + 1
        }

        [Fact]
        public void Modify_ExtraHits_WithCooldownAndCost()
        {
            // A modifier that adds one extra hit but also increases cooldown and cost.
            var db = BuildDb(@"
- id: heavy-multishot
  modify:
    extraHits: 1
    cooldown: 1
    cost: 2
", string.Format(UnitYaml, "heavy-multishot"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(2.0, skill.BaseHits); // base 1.0 + 1
            Assert.Equal(3, skill.Cooldown);   // base 2 + 1
            Assert.Equal(5, skill.Cost);       // base 3 + 2
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

        // ── Exclusive modifier validation ─────────────────────────────────────

        [Fact]
        public void ExclusiveWith_Basic_And_Ultimate_ThrowsWhenCombined()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => BuildDb(@"
- id: basic
  name: Basic
  exclusiveWith:
    - ultimate
  set:
    cost: 0
    cooldown: 0
- id: ultimate
  name: Ultimate
  exclusiveWith:
    - basic
  modify:
    cooldown: 1
", @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
skills:
  - id: sword-strike
    modifiers:
      - basic
      - ultimate
"));
            Assert.Contains("basic", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ultimate", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("test-unit", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sword-strike", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExclusiveWith_Basic_Alone_DoesNotThrow()
        {
            var db = BuildDb(@"
- id: basic
  name: Basic
  exclusiveWith:
    - ultimate
  set:
    cost: 0
    cooldown: 0
- id: ultimate
  name: Ultimate
  exclusiveWith:
    - basic
  modify:
    cooldown: 1
", string.Format(UnitYaml, "basic"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.True(skill.IsBasic);
            Assert.False(skill.IsUltimate);
        }

        [Fact]
        public void ExclusiveWith_Ultimate_Alone_DoesNotThrow()
        {
            var db = BuildDb(@"
- id: basic
  name: Basic
  exclusiveWith:
    - ultimate
  set:
    cost: 0
    cooldown: 0
- id: ultimate
  name: Ultimate
  exclusiveWith:
    - basic
  modify:
    cooldown: 1
", string.Format(UnitYaml, "ultimate"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.False(skill.IsBasic);
            Assert.True(skill.IsUltimate);
        }

        [Fact]
        public void ExclusiveWith_ModifierWithoutExclusiveWith_CombinesFreely()
        {
            // Modifiers that declare no exclusiveWith can be combined with anything.
            var db = BuildDb(@"
- id: mod-a
  name: Mod A
  set:
    cost: 0
- id: mod-b
  name: Mod B
  modify:
    cooldown: -1
", @"
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
");

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(0, skill.Cost);
            Assert.Equal(1, skill.Cooldown); // base 2 + (-1)
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

        // ── Unit-level resistance modifiers ───────────────────────────────────

        private const string UnitYamlWithUnitMod = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
modifiers:
  - {0}
skills:
  - id: sword-strike
";

        [Fact]
        public void UnitModifier_Set_OverridesPhysicalResistance()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      physical: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(50, unit.GetResistance(EffectType.Physical));
        }

        [Fact]
        public void UnitModifier_Set_OverridesVoidResistance()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      void: 100
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(100, unit.GetResistance(EffectType.Void));
        }

        [Fact]
        public void UnitModifier_Modify_AddsToExistingResistance()
        {
            // Unit has physical: 30 in raw data; modifier adds +20 → final 50.
            const string unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
resistances:
  physical: 30
modifiers:
  - test-mod
skills:
  - id: sword-strike
";
            var db = BuildDb(@"
- id: test-mod
  modify:
    resistance:
      physical: 20
", unitYaml);

            var unit = db.GetUnit("test-unit");
            Assert.Equal(50, unit.GetResistance(EffectType.Physical));
        }

        [Fact]
        public void UnitModifier_Modify_AddsNegativeResistance_Weakness()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    resistance:
      fire: -25
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(-25, unit.GetResistance(EffectType.Fire));
        }

        [Fact]
        public void UnitModifier_Set_OverridesDisruptionResistance()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      disruption: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(50, unit.DisruptionResistance);
        }

        [Fact]
        public void UnitModifier_Modify_AddsToDisruptionResistance()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    resistance:
      disruption: 30
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(30, unit.DisruptionResistance);
        }

        [Fact]
        public void UnitModifier_LastSetWins_WhenMultipleModsSetSameResistance()
        {
            const string unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
modifiers:
  - mod-a
  - mod-b
skills:
  - id: sword-strike
";
            var db = BuildDb(@"
- id: mod-a
  set:
    resistance:
      physical: 25
- id: mod-b
  set:
    resistance:
      physical: 75
", unitYaml);

            var unit = db.GetUnit("test-unit");
            Assert.Equal(75, unit.GetResistance(EffectType.Physical)); // last mod wins
        }

        [Fact]
        public void UnitModifier_SetThenModify_ModifyAppliedAfterSet()
        {
            // Set physical resistance to 40, then add +10 → should be 50, not base + 10.
            const string unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
modifiers:
  - set-mod
  - add-mod
skills:
  - id: sword-strike
";
            var db = BuildDb(@"
- id: set-mod
  set:
    resistance:
      physical: 40
- id: add-mod
  modify:
    resistance:
      physical: 10
", unitYaml);

            var unit = db.GetUnit("test-unit");
            Assert.Equal(50, unit.GetResistance(EffectType.Physical));
        }

        [Fact]
        public void UnitModifier_DoesNotAffectSkillVariables()
        {
            // A resistance modifier on a unit should not change skill cost.
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      physical: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var skill = GetSkill(db, "test-unit", "sword-strike");
            Assert.Equal(3, skill.Cost); // base cost unchanged
        }

        [Fact]
        public void UnitModifier_SkillVariables_DoNotAffectUnitResistances()
        {
            // A cost modifier in the unit modifier list should not affect resistances.
            var db = BuildDb(@"
- id: test-mod
  set:
    cost: 0
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(0, unit.GetResistance(EffectType.Physical)); // no resistance set
        }

        [Fact]
        public void UnitModifier_AllElementTypes_AreAppliedIndependently()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      physical: 10
      fire: 20
      cold: 30
      lightning: 40
      holy: 50
      void: 60
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(10, unit.GetResistance(EffectType.Physical));
            Assert.Equal(20, unit.GetResistance(EffectType.Fire));
            Assert.Equal(30, unit.GetResistance(EffectType.Cold));
            Assert.Equal(40, unit.GetResistance(EffectType.Lightning));
            Assert.Equal(50, unit.GetResistance(EffectType.Holy));
            Assert.Equal(60, unit.GetResistance(EffectType.Void));
        }

        // ── Unit-level penetration modifiers ──────────────────────────────────

        [Fact]
        public void UnitModifier_Set_OverridesPhysicalPenetration()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    penetration:
      physical: 30
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(30, unit.GetPenetration(EffectType.Physical));
        }

        [Fact]
        public void UnitModifier_Set_OverridesVoidPenetration()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    penetration:
      void: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(50, unit.GetPenetration(EffectType.Void));
        }

        [Fact]
        public void UnitModifier_Modify_AddsToPenetration()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    penetration:
      fire: 25
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(25, unit.GetPenetration(EffectType.Fire));
        }

        [Fact]
        public void UnitModifier_Set_OverridesDisruptionPenetration()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    penetration:
      disruption: 40
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(40, unit.DisruptionPenetration);
        }

        [Fact]
        public void UnitModifier_Modify_AddsToDisruptionPenetration()
        {
            var db = BuildDb(@"
- id: test-mod
  modify:
    penetration:
      disruption: 20
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(20, unit.DisruptionPenetration);
        }

        [Fact]
        public void UnitModifier_AllPenetrationTypes_AreAppliedIndependently()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    penetration:
      physical: 10
      fire: 20
      cold: 30
      lightning: 40
      holy: 50
      void: 60
      disruption: 70
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(10, unit.GetPenetration(EffectType.Physical));
            Assert.Equal(20, unit.GetPenetration(EffectType.Fire));
            Assert.Equal(30, unit.GetPenetration(EffectType.Cold));
            Assert.Equal(40, unit.GetPenetration(EffectType.Lightning));
            Assert.Equal(50, unit.GetPenetration(EffectType.Holy));
            Assert.Equal(60, unit.GetPenetration(EffectType.Void));
            Assert.Equal(70, unit.DisruptionPenetration);
        }

        [Fact]
        public void UnitModifier_Penetration_DoesNotAffectResistances()
        {
            // A penetration modifier should not change the unit's own resistances.
            var db = BuildDb(@"
- id: test-mod
  set:
    penetration:
      physical: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(0, unit.GetResistance(EffectType.Physical));
        }

        [Fact]
        public void UnitModifier_Resistance_DoesNotAffectPenetrations()
        {
            // A resistance modifier should not affect the unit's penetration.
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      physical: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(0, unit.GetPenetration(EffectType.Physical));
        }

        [Fact]
        public void UnitModifier_NoPenetrationModifier_DefaultIsZero()
        {
            var db = BuildDb(@"
- id: test-mod
  set:
    resistance:
      physical: 50
", string.Format(UnitYamlWithUnitMod, "test-mod"));

            var unit = db.GetUnit("test-unit");
            Assert.Equal(0, unit.GetPenetration(EffectType.Physical));
            Assert.Equal(0, unit.GetPenetration(EffectType.Void));
            Assert.Equal(0, unit.DisruptionPenetration);
        }

        // ── Passive skill stat bonuses ────────────────────────────────────────

        private static ContentDatabase BuildDbWithPassive(string skillsYaml, string passiveSkillId)
        {
            var unitYaml = $@"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
skills:
  - id: sword-strike
  - id: {passiveSkillId}
";
            var files = new Dictionary<string, string>
            {
                ["modifiers.yml"] = "[]",
                ["skills/sword-strike.yml"] = SkillYaml,
                [$"skills/passive-test.yml"] = skillsYaml,
                ["units/test-unit.yml"] = unitYaml,
            };
            return ContentPipeline.Load(new InMemoryContentSource(files));
        }

        [Fact]
        public void PassiveSkill_PhysicalPenetration_AppliedToUnit()
        {
            var db = BuildDbWithPassive(@"
- id: test-passive
  name: Test Passive
  category: Passive
  penetration:
    physical: 20
", "test-passive");

            var unit = db.GetUnit("test-unit");
            Assert.Equal(20, unit.GetPenetration(EffectType.Physical));
        }

        [Fact]
        public void PassiveSkill_FirePenetration_AppliedToUnit()
        {
            var db = BuildDbWithPassive(@"
- id: test-passive
  name: Test Passive
  category: Passive
  penetration:
    fire: 20
", "test-passive");

            var unit = db.GetUnit("test-unit");
            Assert.Equal(20, unit.GetPenetration(EffectType.Fire));
        }

        [Fact]
        public void PassiveSkill_DisruptionResistance_AppliedToUnit()
        {
            var db = BuildDbWithPassive(@"
- id: test-passive
  name: Test Passive
  category: Passive
  resistance:
    disruption: 40
", "test-passive");

            var unit = db.GetUnit("test-unit");
            Assert.Equal(40, unit.DisruptionResistance);
        }

        [Fact]
        public void PassiveSkill_ElementalResistance_AppliedToUnit()
        {
            var db = BuildDbWithPassive(@"
- id: test-passive
  name: Test Passive
  category: Passive
  resistance:
    cold: 30
", "test-passive");

            var unit = db.GetUnit("test-unit");
            Assert.Equal(30, unit.GetResistance(EffectType.Cold));
        }

        [Fact]
        public void PassiveSkill_DisruptionPenetration_AppliedToUnit()
        {
            var db = BuildDbWithPassive(@"
- id: test-passive
  name: Test Passive
  category: Passive
  penetration:
    disruption: 25
", "test-passive");

            var unit = db.GetUnit("test-unit");
            Assert.Equal(25, unit.DisruptionPenetration);
        }

        [Fact]
        public void PassiveSkill_DoesNotAffectNonPassiveStats_WhenCategoryIsAttack()
        {
            // A skill with penetration fields but category Attack should NOT apply passive bonuses.
            var db = BuildDbWithPassive(@"
- id: test-passive
  name: Test Active
  category: Attack
  penetration:
    physical: 50
", "test-passive");

            var unit = db.GetUnit("test-unit");
            Assert.Equal(0, unit.GetPenetration(EffectType.Physical));
        }

        [Fact]
        public void PassiveSkill_StacksWithUnitModifierPenetration()
        {
            // Unit has a modifier with 10% physical penetration, and a passive skill with 20%.
            // Total should be 30%.
            var unitYaml = @"
id: test-unit
name: Test Unit
level: 1
str: 100
wis: 80
agi: 50
modifiers:
  - pen-mod
skills:
  - id: sword-strike
  - id: test-passive
";
            var files = new Dictionary<string, string>
            {
                ["modifiers.yml"] = @"
- id: pen-mod
  modify:
    penetration:
      physical: 10
",
                ["skills/sword-strike.yml"] = SkillYaml,
                ["skills/passive-test.yml"] = @"
- id: test-passive
  name: Test Passive
  category: Passive
  penetration:
    physical: 20
",
                ["units/test-unit.yml"] = unitYaml,
            };
            var db = ContentPipeline.Load(new InMemoryContentSource(files));
            var unit = db.GetUnit("test-unit");
            Assert.Equal(30, unit.GetPenetration(EffectType.Physical));
        }
    }
}
