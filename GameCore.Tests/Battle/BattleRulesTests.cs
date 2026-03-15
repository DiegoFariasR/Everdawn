using System;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class BattleRulesTests
    {
        [Fact]
        public void MaxHp_IsStrTimesOneHundred()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 50);
            Assert.Equal(7000, unit.MaxHp);
        }

        [Fact]
        public void PhysAttack_IsStrTimesEight()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 50);
            Assert.Equal(560, unit.PhysAttack);
        }

        [Fact]
        public void MagicAttack_IsWisTimesEight()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 100, Agi: 50);
            Assert.Equal(800, unit.MagicAttack);
        }

        [Fact]
        public void Attack_UsesPhysical_WhenStrDominates()
        {
            // Str=100 -> Phys=800, Wis=50 -> Magic=400
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 100, Wis: 50, Agi: 50);
            Assert.Equal(unit.PhysAttack, unit.Attack);
            Assert.Equal(800, unit.Attack);
        }

        [Fact]
        public void Attack_UsesMagic_WhenWisDominates()
        {
            // Str=50 -> Phys=400, Wis=100 -> Magic=800
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 50, Wis: 100, Agi: 50);
            Assert.Equal(unit.MagicAttack, unit.Attack);
            Assert.Equal(800, unit.Attack);
        }

        [Fact]
        public void Attack_UsesMagic_WhenEqual()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 100, Wis: 100, Agi: 50);
            Assert.Equal(800, unit.Attack);
        }

        [Fact]
        public void Initiative_IsAgi()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 95);
            Assert.Equal(95, unit.Initiative);
        }

        [Fact]
        public void HitCount_IsOneBasePlusOnePerHundredAgi()
        {
            var low = new BattleUnit("low", "Low", "player", Level: 1, Str: 70, Wis: 0, Agi: 50);
            var high = new BattleUnit("high", "High", "player", Level: 1, Str: 70, Wis: 0, Agi: 120);
            Assert.Equal(1, low.HitCount);  // 1 + 50/100 = 1
            Assert.Equal(2, high.HitCount); // 1 + 120/100 = 2
        }

        [Fact]
        public void HitCount_AtExactlyOneHundredAgi_IsTwo()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 100);
            Assert.Equal(2, unit.HitCount); // 1 + 100/100 = 2
        }

        [Fact]
        public void ResolvedSkills_DefaultsToAttackSkill_WhenNoneProvided()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 50);
            Assert.Single(unit.ResolvedSkills);
            Assert.Equal("attack", unit.ResolvedSkills[0].Id);
            Assert.Equal(0, unit.ResolvedSkills[0].Cost);
        }

        [Fact]
        public void ResolvedSkills_DefaultsToAttackSkill_WhenEmptyListProvided()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 50,
                Skills: Array.Empty<BattleSkill>());
            Assert.Single(unit.ResolvedSkills);
            Assert.Equal("attack", unit.ResolvedSkills[0].Id);
        }

        [Fact]
        public void ResolvedSkills_UsesProvidedSkills_WhenGiven()
        {
            var effects = new[] { new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                new[] { new DamageComponent(EffectType.Physical, new[] { new DamageScaling("str", 1.0) }) }) };
            var skills = new[] { new BattleSkill("custom", "Custom", Cost: 0, DamageMultiplier: 1.5, Effects: effects) };
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 50, Skills: skills);
            Assert.Single(unit.ResolvedSkills);
            Assert.Equal("custom", unit.ResolvedSkills[0].Id);
        }

        [Fact]
        public void DefaultAttackSkill_MultiplierIsOne()
        {
            var unit = new BattleUnit("u1", "Test", "player", Level: 1, Str: 70, Wis: 0, Agi: 50);
            Assert.Equal(1.0, unit.ResolvedSkills[0].DamageMultiplier);
        }
    }
}
