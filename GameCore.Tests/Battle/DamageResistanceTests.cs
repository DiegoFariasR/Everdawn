using System;
using System.Collections.Generic;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class DamageResistanceTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static BattleUnit MakeActor(int str = 100, int wis = 0) =>
            new("actor", "Actor", "player", Level: 1, Str: str, Wis: wis, Agi: 50);

        private static BattleUnit MakeActorWithPenetration(int str = 100, int wis = 0, IReadOnlyDictionary<EffectType, int>? penetrations = null) =>
            new("actor", "Actor", "player", Level: 1, Str: str, Wis: wis, Agi: 50, Penetrations: penetrations);

        private static BattleUnit MakeTarget(IReadOnlyDictionary<EffectType, int>? resistances = null) =>
            new("target", "Target", "enemy", Level: 1, Str: 100, Wis: 0, Agi: 50, Resistances: resistances);

        private static DamageResult Compute(BattleUnit actor, BattleUnit target, EffectType type, int seed = 0) =>
            DamageCalc.Compute(actor, target, type, 1.0, 1.0, new Random(seed));

        // ── DamageResult structure ────────────────────────────────────────────

        [Fact]
        public void Result_RecordsEffectType()
        {
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Void);
            Assert.Equal(EffectType.Void, result.EffectType);
        }

        // ── No resistance ─────────────────────────────────────────────────────

        [Fact]
        public void NoResistance_FinalDamageEqualsRawDamage()
        {
            var result = Compute(MakeActor(str: 100), MakeTarget(), EffectType.Physical);
            Assert.Equal(result.RawDamage, result.FinalDamage);
        }

        // ── Resistance percentages ────────────────────────────────────────────

        [Fact]
        public void FiftyPercentResistance_FinalDamageIsHalf()
        {
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 50 });
            var result = Compute(MakeActor(str: 100), target, EffectType.Physical);
            Assert.Equal((int)(result.RawDamage * 0.5), result.FinalDamage);
        }

        [Fact]
        public void HundredPercentResistance_CapsAt90_DealsMinimumDamage()
        {
            // Resistance is capped at 90%: even immune units always take ≥10% damage.
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 100 });
            var result = Compute(MakeActor(str: 100), target, EffectType.Physical);
            Assert.Equal((int)(result.RawDamage * 0.10), result.FinalDamage);
            Assert.True(result.FinalDamage > 0);
        }

        [Fact]
        public void NegativeResistance_FinalDamageIsIncreased()
        {
            // -50 resistance = 50% weakness → 1.5× damage
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = -50 });
            var result = Compute(MakeActor(str: 100), target, EffectType.Physical);
            Assert.Equal((int)(result.RawDamage * 1.5), result.FinalDamage);
        }

        // ── Type separation ───────────────────────────────────────────────────

        [Fact]
        public void PhysicalResistance_DoesNotAffectVoidDamage()
        {
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 100 });
            var result = Compute(MakeActor(wis: 100), target, EffectType.Void);
            Assert.Equal(result.RawDamage, result.FinalDamage);
            Assert.True(result.FinalDamage > 0);
        }

        [Fact]
        public void VoidResistance_DoesNotAffectPhysicalDamage()
        {
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Void] = 100 });
            var result = Compute(MakeActor(str: 100), target, EffectType.Physical);
            Assert.Equal(result.RawDamage, result.FinalDamage);
            Assert.True(result.FinalDamage > 0);
        }

        // ── Attack stat selection ─────────────────────────────────────────────

        [Fact]
        public void PhysicalType_UsesPhysAttack()
        {
            var actor = MakeActor(str: 0, wis: 100);
            var physResult = Compute(actor, MakeTarget(), EffectType.Physical);
            var voidResult = Compute(actor, MakeTarget(), EffectType.Void);
            Assert.True(voidResult.RawDamage > physResult.RawDamage);
        }

        [Fact]
        public void NonPhysicalType_UsesMagicAttack()
        {
            var actor = MakeActor(str: 100, wis: 0);
            var physResult = Compute(actor, MakeTarget(), EffectType.Physical);
            var voidResult = Compute(actor, MakeTarget(), EffectType.Void);
            Assert.True(physResult.RawDamage > voidResult.RawDamage);
        }

        // ── Pipeline steps ────────────────────────────────────────────────────

        [Fact]
        public void Pipeline_HasExactlyTwoStepsByDefault()
        {
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Physical);
            Assert.Equal(2, result.Steps.Count);
        }

        [Fact]
        public void Pipeline_FirstStep_IsNamedBase()
        {
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Physical);
            Assert.Equal("Base", result.Steps[0].Name);
        }

        [Fact]
        public void Pipeline_BaseStep_ValueBeforeIsBaseAttackStat()
        {
            // Str 100 → PhysAttack = 800
            var actor = MakeActor(str: 100);
            var result = Compute(actor, MakeTarget(), EffectType.Physical);
            Assert.Equal(actor.PhysAttack, result.Steps[0].ValueBefore);
        }

        [Fact]
        public void Pipeline_SecondStep_IsNamedResistance()
        {
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Physical);
            Assert.Equal("Resistance", result.Steps[1].Name);
        }

        [Fact]
        public void Pipeline_ResistanceStep_ValueBeforeMatchesRawDamage()
        {
            // The Resistance step's input must equal the Base step's output.
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Physical);
            Assert.Equal(result.Steps[0].ValueAfter, result.Steps[1].ValueBefore);
        }

        [Fact]
        public void Pipeline_ResistanceStep_ValueAfterMatchesFinalDamage()
        {
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Physical);
            Assert.Equal(result.Steps[1].ValueAfter, result.FinalDamage);
        }

        [Fact]
        public void Pipeline_StepsAreContiguous_EachStepOutputMatchesNextStepInput()
        {
            // General invariant: every consecutive pair of steps must be connected.
            var result = Compute(MakeActor(), MakeTarget(), EffectType.Physical);
            for (int i = 0; i < result.Steps.Count - 1; i++)
                Assert.Equal(result.Steps[i].ValueAfter, result.Steps[i + 1].ValueBefore);
        }

        // ── BattleUnit helpers ────────────────────────────────────────────────

        [Fact]
        public void GetResistance_ReturnsZeroWhenNoResistancesDefined()
        {
            var unit = MakeTarget();
            Assert.Equal(0, unit.GetResistance(EffectType.Physical));
            Assert.Equal(0, unit.GetResistance(EffectType.Void));
        }

        // ── Penetration ───────────────────────────────────────────────────────

        [Fact]
        public void GetPenetration_ReturnsZeroWhenNoPenetrationsDefined()
        {
            var actor = MakeActor();
            Assert.Equal(0, actor.GetPenetration(EffectType.Physical));
            Assert.Equal(0, actor.GetPenetration(EffectType.Void));
        }

        [Fact]
        public void Penetration_ReducesEffectiveResistance()
        {
            // Target has 50% resistance, actor has 20% penetration → net 30% resistance.
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 50 });
            var actor = MakeActorWithPenetration(penetrations: new Dictionary<EffectType, int> { [EffectType.Physical] = 20 });
            var result = Compute(actor, target, EffectType.Physical);
            // Net resistance = 30% → final = raw * 0.70
            Assert.Equal((int)(result.RawDamage * 0.70), result.FinalDamage);
        }

        [Fact]
        public void Penetration_CanReduceResistanceBelowZero_CausingExtraDamage()
        {
            // Target has 10% resistance, actor has 30% penetration → net -20% (weakness).
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 10 });
            var actor = MakeActorWithPenetration(penetrations: new Dictionary<EffectType, int> { [EffectType.Physical] = 30 });
            var result = Compute(actor, target, EffectType.Physical);
            // Net resistance = -20% → final = raw * 1.20
            Assert.Equal((int)(result.RawDamage * 1.20), result.FinalDamage);
        }

        [Fact]
        public void Penetration_WithNoResistance_CausesExtraDamage()
        {
            // Target has 0% resistance, actor has 50% penetration → net -50% (weakness).
            var actor = MakeActorWithPenetration(penetrations: new Dictionary<EffectType, int> { [EffectType.Physical] = 50 });
            var result = Compute(actor, MakeTarget(), EffectType.Physical);
            // Net resistance = -50% → final = raw * 1.50
            Assert.Equal((int)(result.RawDamage * 1.50), result.FinalDamage);
        }

        [Fact]
        public void Penetration_OnlyAffectsMatchingDamageType()
        {
            // Actor has physical penetration, dealing void damage → penetration ignored.
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Void] = 50 });
            var actor = MakeActorWithPenetration(wis: 100, penetrations: new Dictionary<EffectType, int> { [EffectType.Physical] = 50 });
            var result = Compute(actor, target, EffectType.Void);
            // Physical penetration does not reduce void resistance → net 50% resistance.
            Assert.Equal((int)(result.RawDamage * 0.50), result.FinalDamage);
        }

        [Fact]
        public void Penetration_FullPenetrationNegatesImmunity()
        {
            // Target is immune (100% resistance), actor has 100% penetration → net 0%.
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 100 });
            var actor = MakeActorWithPenetration(penetrations: new Dictionary<EffectType, int> { [EffectType.Physical] = 100 });
            var result = Compute(actor, target, EffectType.Physical);
            Assert.Equal(result.RawDamage, result.FinalDamage);
        }

        [Fact]
        public void NaturalEffectType_IsVoidWhenWisHigher()
        {
            var mage = MakeActor(str: 10, wis: 100);
            Assert.Equal(EffectType.Void, mage.NaturalEffectType);
        }

        [Fact]
        public void NaturalEffectType_IsPhysicalWhenStrHigher()
        {
            var warrior = MakeActor(str: 100, wis: 10);
            Assert.Equal(EffectType.Physical, warrior.NaturalEffectType);
        }
    }
}
