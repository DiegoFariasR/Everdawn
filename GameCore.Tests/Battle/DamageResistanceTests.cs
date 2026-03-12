using GameCore.Battle;

namespace GameCore.Tests.Battle
{
    public class DamageResistanceTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static BattleUnit MakeActor(int str = 100, int wis = 0) =>
            new("actor", "Actor", "player", Level: 1, Str: str, Wis: wis, Agi: 50);

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
        public void HundredPercentResistance_FinalDamageIsZero()
        {
            var target = MakeTarget(new Dictionary<EffectType, int> { [EffectType.Physical] = 100 });
            var result = Compute(MakeActor(str: 100), target, EffectType.Physical);
            Assert.Equal(0, result.FinalDamage);
            Assert.True(result.RawDamage > 0);
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
