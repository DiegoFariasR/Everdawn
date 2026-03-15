using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class FuryTraitTests
    {
        // ── BattleUnit derived props ──────────────────────────────────────────

        [Fact]
        public void Fury_MaxFury_IsOneHundred()
        {
            var unit = MakeUnit(traits: new[] { BattleTrait.FuryUser });
            Assert.Equal(100, unit.MaxBars.TryGetValue("fury", out int v) ? v : 0);
        }

        [Fact]
        public void Fury_InitialFury_IsZero()
        {
            var unit = MakeUnit(traits: new[] { BattleTrait.FuryUser });
            Assert.Equal(0, unit.InitialBars.TryGetValue("fury", out int v) ? v : -1);
        }

        [Fact]
        public void NoTrait_MaxFury_NotPresent()
        {
            var unit = MakeUnit(traits: null);
            Assert.False(unit.MaxBars.ContainsKey("fury"));
        }

        [Fact]
        public void NoTrait_InitialFury_NotPresent()
        {
            var unit = MakeUnit(traits: null);
            Assert.False(unit.InitialBars.ContainsKey("fury"));
        }

        // ── Initial state ─────────────────────────────────────────────────────

        [Fact]
        public void FuryUnit_StartsAtZeroFury()
        {
            var session = StartFuryBattle();
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            Assert.Equal(0, state.GetBar("fury"));
        }

        // ── Fury gain: from damage taken ─────────────────────────────────────

        [Fact]
        public void FuryUnit_GainsFuryWhenHit()
        {
            // Enemy hits the Fury unit. Unit should gain Fury.
            var session = StartFuryBattle(enemyGoesFirst: true);
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            Assert.True(state.GetBar("fury") > 0, "Fury should be > 0 after taking a hit.");
        }

        [Fact]
        public void FuryGainFromHit_IncludesFlatComponent()
        {
            // Any hit must grant at least the flat base gain.
            Assert.True(FurySystem.ComputeHitGain(hpLost: 0, maxHp: 1000) == FurySystem.FlatGainOnHit);
        }

        [Fact]
        public void FuryGainFromHit_IncludesHpPctBonus()
        {
            // Losing 50% max HP should give flat + 50 × HpPctGainPerPoint.
            int maxHp = 1000;
            int hpLost = 500; // 50%
            int expected = FurySystem.FlatGainOnHit + (int)(50.0 * FurySystem.HpPctGainPerPoint);
            Assert.Equal(expected, FurySystem.ComputeHitGain(hpLost, maxHp));
        }

        [Fact]
        public void FuryGainFromHit_ScalesWithHpLost()
        {
            // Higher HP% lost → more Fury gained.
            int smallGain = FurySystem.ComputeHitGain(hpLost: 100, maxHp: 1000);
            int bigGain = FurySystem.ComputeHitGain(hpLost: 500, maxHp: 1000);
            Assert.True(bigGain > smallGain, "Larger HP% loss should yield more Fury.");
        }

        [Fact]
        public void FuryGain_IsCappedAtMax()
        {
            // Even a huge hit cannot push Fury above 100.
            var session = StartFuryBattle(enemyGoesFirst: true, enemyStr: 9999);
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            Assert.True(state.GetBar("fury") <= FurySystem.MaxBar);
        }

        // ── Fury gain: from STR skill use ────────────────────────────────────

        [Fact]
        public void FuryUnit_GainsFuryFromDamagingSkillUse()
        {
            // Using any damaging skill grants SkillUseGain fury.
            var session = StartFuryBattle();
            session.TryExecute(new PlayerActionCommand("str-skill", "target"));
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            // Fury should be SkillUseGain (no decay yet, enemy not acted).
            Assert.Equal(FurySystem.SkillUseGain, state.GetBar("fury"));
        }

        [Fact]
        public void FuryUnit_GainsFuryFromAnyDamagingSkill()
        {
            // The non-str-skill also deals damage — it must also grant fury now.
            var session = StartFuryBattle();
            session.TryExecute(new PlayerActionCommand("non-str-skill", "target"));
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            Assert.Equal(FurySystem.SkillUseGain, state.GetBar("fury"));
        }

        [Fact]
        public void FuryGain_IsOncePerExecution_NotPerHit()
        {
            // A skill with multiple hits must still only grant SkillUseGain once.
            var session = StartFuryBattle(playerAgi: 200); // 3 hits
            session.TryExecute(new PlayerActionCommand("str-skill", "target"));
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            // Should be exactly SkillUseGain, not SkillUseGain * hits.
            Assert.Equal(FurySystem.SkillUseGain, state.GetBar("fury"));
        }

        // ── Fury decay ────────────────────────────────────────────────────────

        [Fact]
        public void FuryDecay_ReducesFuryAtStartOfNextTurn()
        {
            // Give the player unit some fury, then end their turn and let the enemy act.
            // On the player's next turn, fury should have decayed.
            var session = StartFuryBattle();

            // Inject fury = 50 via Resume so we have a known baseline.
            var snap = new[]
            {
                new UnitState("fury-unit", 100 * 100, true, new Dictionary<string, int> { ["fury"] = 50 }),
                new UnitState("target", 100 * 100, true, null),
            };
            session.TryExecute(new ResumeFromSnapshotCommand(snap, LastActorId: "target", AtStep: 0));

            // Player acts with a damaging skill — gains SkillUseGain fury on top of the injected value.
            session.TryExecute(new PlayerActionCommand("str-skill", "target"));

            // After the player's turn: enemy acts (hits player with a weak strike → flat fury gain only).
            // Then player's next StartOfTurn fires decay.
            // fury-unit Str:100 → MaxHp=10000. Enemy Str:10 → PhysAttack=80. HP%=0.8% → bonus=0.
            // Expected: 50 + SkillUseGain + FlatGainOnHit - DecayPerTurn.
            var state = session.GetView().Units.First(u => u.UnitId == "fury-unit");
            int expectedAfterHitAndDecay = 50 + FurySystem.SkillUseGain + FurySystem.FlatGainOnHit - FurySystem.DecayPerTurn;
            Assert.Equal(expectedAfterHitAndDecay, state.GetBar("fury"));
        }

        [Fact]
        public void FuryDecay_ClampsToZero()
        {
            Assert.Equal(0, FurySystem.ApplyDecay(0));
            Assert.Equal(0, FurySystem.ApplyDecay(5));
        }

        [Fact]
        public void FuryDecay_DecayPerTurnIsCorrect()
        {
            Assert.Equal(100 - FurySystem.DecayPerTurn, FurySystem.ApplyDecay(100));
        }

        // ── Fury damage scaling ───────────────────────────────────────────────

        [Fact]
        public void FuryDamageBonus_AtZeroFury_IsOne()
        {
            Assert.Equal(1.0, FurySystem.ComputeDamageBonus(fury: 0, furyDamageScale: 0.5));
        }

        [Fact]
        public void FuryDamageBonus_AtMaxFury_IsOnePlusFuryDamageScale()
        {
            double expected = 1.0 + 0.5;
            Assert.Equal(expected, FurySystem.ComputeDamageBonus(fury: 100, furyDamageScale: 0.5));
        }

        [Fact]
        public void FuryDamageBonus_AtHalfFury_IsHalfScale()
        {
            double expected = 1.0 + 0.5 * 0.5;
            Assert.Equal(expected, FurySystem.ComputeDamageBonus(fury: 50, furyDamageScale: 0.5));
        }

        [Fact]
        public void DamagingSkill_DealsBonusDamageAtHighFury()
        {
            // At Fury = 0, a skill deals its base damage.
            // At Fury = 100, a unit with FuryDamageScale = 0.4 deals 40% more.
            int lowFuryDmg = RunDamagingSkillDamage(injectedFury: 0);
            int highFuryDmg = RunDamagingSkillDamage(injectedFury: 100);
            Assert.True(highFuryDmg > lowFuryDmg,
                $"High Fury ({highFuryDmg}) should deal more damage than low Fury ({lowFuryDmg}).");
        }

        // ── BattleUnit FuryDamageScale field ─────────────────────────────────

        [Fact]
        public void BattleUnit_FuryDamageScale_DefaultIsZero()
        {
            var unit = MakeUnit(traits: null);
            Assert.Equal(0.0, unit.FuryDamageScale);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Runs a damaging skill action and returns total damage at the given injected Fury.
        private static int RunDamagingSkillDamage(int injectedFury)
        {
            var session = StartFuryBattle(seed: 42);
            if (injectedFury != 0)
            {
                var snap = new[]
                {
                    new UnitState("fury-unit", 100 * 100, true, new Dictionary<string, int> { ["fury"] = injectedFury }),
                    new UnitState("target", 100 * 100, true, null),
                };
                session.TryExecute(new ResumeFromSnapshotCommand(snap, LastActorId: "target", AtStep: 0));
            }
            var result = session.TryExecute(new PlayerActionCommand("str-skill", "target"));
            return result.Events.Where(e => e.TargetId == "target" && e.Value > 0).Sum(e => e.Value);
        }

        // Player: str-skill (damaging), non-str-skill (also damaging). FuryDamageScale is on the unit.
        private static BattleSession StartFuryBattle(
            bool enemyGoesFirst = false,
            int playerAgi = 50,
            int enemyStr = 10,
            int seed = 0)
        {
            int playerAgiForOrder = enemyGoesFirst ? 1 : 100;
            var setup = new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    new("fury-unit", "Barbarian", "player", Level: 1, Str: 100, Wis: 0, Agi: playerAgiForOrder,
                        FuryDamageScale: 0.4,
                        Skills: new BattleSkill[]
                        {
                            new("str-skill", "Mace Strike", Cost: 0, TotalDamageMultiplier: 1.0,
                                Effects: PhysEffect(),
                                Modifiers: new[] { "basic" }, ModifierTags: new[] { "basic" }),
                            new("non-str-skill", "Support Bash", Cost: 0, TotalDamageMultiplier: 1.0,
                                Effects: PhysEffect()),
                        },
                        Traits: new[] { BattleTrait.FuryUser }),
                },
                EnemyUnits = new List<BattleUnit>
                {
                    new("target", "Dummy", "enemy", Level: 1, Str: enemyStr, Wis: 0, Agi: enemyGoesFirst ? 200 : 1,
                        Skills: new BattleSkill[]
                        {
                            new("def-basic", "Slam", Cost: 0, TotalDamageMultiplier: 1.0,
                                Effects: PhysEffect(),
                                Modifiers: new[] { "basic" }, ModifierTags: new[] { "basic" }),
                        }),
                },
            };
            var session = new BattleSession(seed: seed);
            session.Start(setup);
            return session;
        }

        private static BattleUnit MakeUnit(IReadOnlyList<BattleTrait>? traits) =>
            new("unit", "Unit", "player", Level: 1, Str: 50, Wis: 50, Agi: 50, Traits: traits);

        private static SkillEffect[] PhysEffect() =>
            new SkillEffect[] { new(EffectKind.Damage, BattleSkillTarget.Enemy,
                new DamageComponent[] { new(EffectType.Physical, new DamageScaling[] { new("str", 1.0) }) }) };
    }
}
