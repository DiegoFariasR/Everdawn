using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Content;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for the reusable buff definition system.
    /// Verifies that buff-definitions.yml is loaded correctly, that skills can reference
    /// buff definitions by ID via <c>effectRef</c>, and that the compiled effect stats
    /// are correct.
    /// </summary>
    public class BuffDefinitionTests
    {
        private static readonly ContentDatabase Content =
            ContentPipeline.Load(TestContentSource.Default);

        // ── Buff definition loading ───────────────────────────────────────────

        [Fact]
        public void AllFourBuffDefinitions_AreLoadedFromContent()
        {
            var ids = Content.AllBuffDefinitions.Select(d => d.Id).ToHashSet();
            Assert.Contains("attack-up", ids);
            Assert.Contains("defense-up", ids);
            Assert.Contains("attack-down", ids);
            Assert.Contains("defense-down", ids);
        }

        [Fact]
        public void AttackUp_HasCorrectDuration()
        {
            var def = Content.GetBuffDefinition("attack-up");
            Assert.Equal(2, def.Duration);
            Assert.Equal(EffectDurationKind.ForTargetTurns, def.DurationKind);
            Assert.Equal(EffectStackingPolicy.RefreshDuration, def.StackingPolicy);
        }

        [Fact]
        public void AttackUp_AppliesToAllDamageTypes()
        {
            var def = Content.GetBuffDefinition("attack-up");
            Assert.NotNull(def.DamageDealtMultiplier);

            foreach (var effectType in System.Enum.GetValues(typeof(EffectType)))
            {
                var et = (EffectType)effectType;
                Assert.True(
                    def.DamageDealtMultiplier!.TryGetValue(et, out var mult),
                    $"attack-up should have a DamageDealtMultiplier for {et}");
                Assert.Equal(1.3, mult, precision: 6);
            }
        }

        [Fact]
        public void DefenseUp_ReducesDamageTakenForAllTypes()
        {
            var def = Content.GetBuffDefinition("defense-up");
            Assert.NotNull(def.DamageTakenMultiplierByType);

            foreach (var effectType in System.Enum.GetValues(typeof(EffectType)))
            {
                var et = (EffectType)effectType;
                Assert.True(
                    def.DamageTakenMultiplierByType!.TryGetValue(et, out var mult),
                    $"defense-up should have a DamageTakenMultiplier for {et}");
                Assert.Equal(0.75, mult, precision: 6);
            }
        }

        [Fact]
        public void AttackDown_ReducesDamageDealtForAllTypes()
        {
            var def = Content.GetBuffDefinition("attack-down");
            Assert.NotNull(def.DamageDealtMultiplier);

            foreach (var effectType in System.Enum.GetValues(typeof(EffectType)))
            {
                var et = (EffectType)effectType;
                Assert.True(
                    def.DamageDealtMultiplier!.TryGetValue(et, out var mult),
                    $"attack-down should have a DamageDealtMultiplier for {et}");
                Assert.Equal(0.7, mult, precision: 6);
            }
        }

        [Fact]
        public void DefenseDown_ReducesResistanceForAllTypes()
        {
            var def = Content.GetBuffDefinition("defense-down");
            Assert.NotNull(def.ResistanceModifierByType);

            foreach (var effectType in System.Enum.GetValues(typeof(EffectType)))
            {
                var et = (EffectType)effectType;
                Assert.True(
                    def.ResistanceModifierByType!.TryGetValue(et, out var res),
                    $"defense-down should have a ResistanceModifier for {et}");
                Assert.Equal(-20, res);
            }
        }

        // ── EffectRef resolution ──────────────────────────────────────────────

        [Fact]
        public void BattleCrySkill_HasAttackUpEffectDefinition()
        {
            var battleCry = Content.GetSkill("battle-cry");
            var effect = battleCry.Effects.Single(e => e.Kind == EffectKind.ApplyEffect);

            Assert.NotNull(effect.EffectDefinition);
            Assert.Equal("attack-up", effect.EffectDefinition!.Id);
            Assert.Equal("Attack Up", effect.EffectDefinition.Name);
        }

        [Fact]
        public void BattleCrySkill_EffectDefinition_AppliesToAllDamageTypes()
        {
            var battleCry = Content.GetSkill("battle-cry");
            var def = battleCry.Effects.Single(e => e.Kind == EffectKind.ApplyEffect).EffectDefinition!;

            Assert.NotNull(def.DamageDealtMultiplier);
            foreach (var effectType in System.Enum.GetValues(typeof(EffectType)))
            {
                var et = (EffectType)effectType;
                Assert.True(def.DamageDealtMultiplier!.ContainsKey(et),
                    $"battle-cry's attack-up effect should cover {et}");
            }
        }

        // ── Unit assignment ───────────────────────────────────────────────────

        [Fact]
        public void Warrior_HasBattleCry()
        {
            var warrior = Content.GetUnit("warrior");
            Assert.Contains(warrior.Skills ?? System.Array.Empty<BattleSkill>(), s => s.Id == "battle-cry");
        }

        [Fact]
        public void Mage_HasBattleCry()
        {
            var mage = Content.GetUnit("mage");
            Assert.Contains(mage.Skills ?? System.Array.Empty<BattleSkill>(), s => s.Id == "battle-cry");
        }
    }
}
