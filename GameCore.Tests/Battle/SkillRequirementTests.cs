#nullable enable
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for skill trait and equipment-type requirements.
    /// Verifies that <see cref="BattleSkill.MeetsRequirements"/> gates skill availability correctly
    /// and that the battle session exposes matching <see cref="PendingInputView.AvailableSkillIds"/>.
    /// </summary>
    public class SkillRequirementTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static SkillEffect[] PhysEffect() =>
            new SkillEffect[]
            {
                new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                    new DamageComponent[] { new DamageComponent(EffectType.Physical, new DamageScaling[] { new DamageScaling("str", 1.0) }) })
            };

        private static BattleUnit MakeUnit(
            EquipmentType equipmentType = EquipmentType.None,
            IReadOnlyList<BattleTrait>? traits = null) =>
            new BattleUnit("u1", "Unit", "player", Level: 1, Str: 50, Wis: 50, Agi: 50,
                EquipmentType: equipmentType, Traits: traits);

        // ── MeetsRequirements unit tests ──────────────────────────────────────

        [Fact]
        public void MeetsRequirements_NoRequirements_AlwaysTrue()
        {
            var skill = new BattleSkill("s", "S", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect());
            var unit = MakeUnit();
            Assert.True(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_RequiredEquipmentType_TrueWhenMatches()
        {
            var skill = new BattleSkill("mace", "Mace Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredEquipmentTypes: new[] { EquipmentType.Blunt });
            var unit = MakeUnit(equipmentType: EquipmentType.Blunt);
            Assert.True(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_RequiredEquipmentType_FalseWhenMismatch()
        {
            var skill = new BattleSkill("mace", "Mace Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredEquipmentTypes: new[] { EquipmentType.Blunt });
            var unit = MakeUnit(equipmentType: EquipmentType.Slash);
            Assert.False(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_RequiredEquipmentType_FalseWhenNone()
        {
            var skill = new BattleSkill("mace", "Mace Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredEquipmentTypes: new[] { EquipmentType.Blunt });
            var unit = MakeUnit(equipmentType: EquipmentType.None);
            Assert.False(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_RequiredTrait_TrueWhenUnitHasTrait()
        {
            var skill = new BattleSkill("bolt", "Magic Bolt", Cost: 10, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredTraits: new[] { BattleTrait.MagicUser });
            var unit = MakeUnit(traits: new[] { BattleTrait.MagicUser });
            Assert.True(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_RequiredTrait_FalseWhenUnitLacksTrait()
        {
            var skill = new BattleSkill("bolt", "Magic Bolt", Cost: 10, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredTraits: new[] { BattleTrait.MagicUser });
            var unit = MakeUnit(traits: new[] { BattleTrait.Fury });
            Assert.False(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_RequiredTrait_FalseWhenNoTraits()
        {
            var skill = new BattleSkill("bolt", "Magic Bolt", Cost: 10, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredTraits: new[] { BattleTrait.MagicUser });
            var unit = MakeUnit();
            Assert.False(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_BothRequirements_FalseWhenOnlyEquipmentMet()
        {
            var skill = new BattleSkill("s", "S", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect(),
                RequiredTraits: new[] { BattleTrait.MagicUser }, RequiredEquipmentTypes: new[] { EquipmentType.Staff });
            var unit = MakeUnit(equipmentType: EquipmentType.Staff);
            Assert.False(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_BothRequirements_FalseWhenOnlyTraitMet()
        {
            var skill = new BattleSkill("s", "S", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect(),
                RequiredTraits: new[] { BattleTrait.MagicUser }, RequiredEquipmentTypes: new[] { EquipmentType.Staff });
            var unit = MakeUnit(traits: new[] { BattleTrait.MagicUser });
            Assert.False(skill.MeetsRequirements(unit));
        }

        [Fact]
        public void MeetsRequirements_BothRequirements_TrueWhenBothMet()
        {
            var skill = new BattleSkill("s", "S", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect(),
                RequiredTraits: new[] { BattleTrait.MagicUser }, RequiredEquipmentTypes: new[] { EquipmentType.Staff });
            var unit = MakeUnit(equipmentType: EquipmentType.Staff, traits: new[] { BattleTrait.MagicUser });
            Assert.True(skill.MeetsRequirements(unit));
        }

        // ── AvailableSkillIds integration tests ───────────────────────────────

        [Fact]
        public void AvailableSkillIds_ExcludesSkillWithUnmetEquipmentRequirement()
        {
            var basicSkill = new BattleSkill("basic", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), Modifiers: new[] { "basic" });
            var maceSkill = new BattleSkill("mace-strike", "Mace Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredEquipmentTypes: new[] { EquipmentType.Blunt });

            // Unit with Slash weapon — cannot use mace-strike.
            var player = new BattleUnit("p", "Player", "player", Level: 1, Str: 100, Wis: 0, Agi: 50,
                EquipmentType: EquipmentType.Slash, Skills: new[] { basicSkill, maceSkill });
            var enemy = new BattleUnit("e", "Enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 10,
                Skills: new[] { basicSkill });

            var setup = new BattleSetup { PlayerUnits = new[] { player }, EnemyUnits = new[] { enemy } };
            var session = new BattleSession(seed: 42);
            session.Start(setup);

            var view = session.GetView();
            var available = view.PendingInput!.AvailableSkillIds;

            Assert.Contains("basic", available);
            Assert.DoesNotContain("mace-strike", available);
        }

        [Fact]
        public void AvailableSkillIds_IncludesSkillWithMetEquipmentRequirement()
        {
            var basicSkill = new BattleSkill("basic", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), Modifiers: new[] { "basic" });
            var maceSkill = new BattleSkill("mace-strike", "Mace Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredEquipmentTypes: new[] { EquipmentType.Blunt });

            // Unit with Blunt weapon — can use mace-strike.
            var player = new BattleUnit("p", "Player", "player", Level: 1, Str: 100, Wis: 0, Agi: 50,
                EquipmentType: EquipmentType.Blunt, Skills: new[] { basicSkill, maceSkill });
            var enemy = new BattleUnit("e", "Enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 10,
                Skills: new[] { basicSkill });

            var setup = new BattleSetup { PlayerUnits = new[] { player }, EnemyUnits = new[] { enemy } };
            var session = new BattleSession(seed: 42);
            session.Start(setup);

            var view = session.GetView();
            var available = view.PendingInput!.AvailableSkillIds;

            Assert.Contains("basic", available);
            Assert.Contains("mace-strike", available);
        }

        [Fact]
        public void AvailableSkillIds_ExcludesSkillWithUnmetTraitRequirement()
        {
            var basicSkill = new BattleSkill("basic", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), Modifiers: new[] { "basic" });
            var spellSkill = new BattleSkill("bolt", "Bolt", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredTraits: new[] { BattleTrait.MagicUser });

            // Non-magic unit — cannot use bolt.
            var player = new BattleUnit("p", "Player", "player", Level: 1, Str: 100, Wis: 0, Agi: 50,
                Skills: new[] { basicSkill, spellSkill });
            var enemy = new BattleUnit("e", "Enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 10,
                Skills: new[] { basicSkill });

            var setup = new BattleSetup { PlayerUnits = new[] { player }, EnemyUnits = new[] { enemy } };
            var session = new BattleSession(seed: 42);
            session.Start(setup);

            var view = session.GetView();
            var available = view.PendingInput!.AvailableSkillIds;

            Assert.Contains("basic", available);
            Assert.DoesNotContain("bolt", available);
        }

        [Fact]
        public void PlayerAction_RequirementNotMet_ReturnsRequirementNotMetError()
        {
            var basicSkill = new BattleSkill("basic", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), Modifiers: new[] { "basic" });
            var maceSkill = new BattleSkill("mace-strike", "Mace Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), RequiredEquipmentTypes: new[] { EquipmentType.Blunt });

            // Unit without Blunt weapon tries to use mace-strike.
            var player = new BattleUnit("p", "Player", "player", Level: 1, Str: 100, Wis: 0, Agi: 50,
                EquipmentType: EquipmentType.Slash, Skills: new[] { basicSkill, maceSkill });
            var enemy = new BattleUnit("e", "Enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 10,
                Skills: new[] { basicSkill });

            var setup = new BattleSetup { PlayerUnits = new[] { player }, EnemyUnits = new[] { enemy } };
            var session = new BattleSession(seed: 42);
            session.Start(setup);

            var result = session.TryExecute(new PlayerActionCommand("mace-strike", "e"));
            Assert.False(result.Accepted);
            Assert.Equal(ValidationErrorCode.RequirementNotMet, result.Error!.Code);
        }

        // ── Content-level requirement tests ───────────────────────────────────

        [Fact]
        public void Content_RogueConcentrate_RequiresFocusTrait()
        {
            var db = ContentPipeline.Load(TestContentSource.Default);
            var skill = db.GetSkill("rogue-concentrate");
            Assert.Contains(BattleTrait.Focus, skill.RequiredTraits);
        }

        [Fact]
        public void Content_RogueConcentrate_UnavailableWithoutFocusTrait()
        {
            var db = ContentPipeline.Load(TestContentSource.Default);
            var basicSkill = new BattleSkill("basic", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), Modifiers: new[] { "basic" });
            var concentrateSkill = db.GetSkill("rogue-concentrate");

            // Unit without Focus trait — cannot use rogue-concentrate.
            var player = new BattleUnit("p", "Player", "player", Level: 1, Str: 100, Wis: 0, Agi: 50,
                Skills: new[] { basicSkill, concentrateSkill });
            var enemy = new BattleUnit("e", "Enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 10,
                Skills: new[] { basicSkill });

            var setup = new BattleSetup { PlayerUnits = new[] { player }, EnemyUnits = new[] { enemy } };
            var session = new BattleSession(seed: 42);
            session.Start(setup);

            var available = session.GetView().PendingInput!.AvailableSkillIds;
            Assert.DoesNotContain("rogue-concentrate", available);
        }

        [Fact]
        public void Content_RogueConcentrate_AvailableWithFocusTrait()
        {
            var db = ContentPipeline.Load(TestContentSource.Default);
            var basicSkill = new BattleSkill("basic", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: PhysEffect(), Modifiers: new[] { "basic" });
            var concentrateSkill = db.GetSkill("rogue-concentrate");

            // Unit with Focus trait — can use rogue-concentrate.
            var player = new BattleUnit("p", "Player", "player", Level: 1, Str: 100, Wis: 0, Agi: 50,
                Traits: new[] { BattleTrait.Focus }, Skills: new[] { basicSkill, concentrateSkill });
            var enemy = new BattleUnit("e", "Enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 10,
                Skills: new[] { basicSkill });

            var setup = new BattleSetup { PlayerUnits = new[] { player }, EnemyUnits = new[] { enemy } };
            var session = new BattleSession(seed: 42);
            session.Start(setup);

            var available = session.GetView().PendingInput!.AvailableSkillIds;
            Assert.Contains("rogue-concentrate", available);
        }
    }
}
