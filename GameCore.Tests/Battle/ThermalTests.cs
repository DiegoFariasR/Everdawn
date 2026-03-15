using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using GameCore.Scenarios;
using GameCore.Tests;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class ThermalTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // ThermalSystem unit tests (pure math, no RNG)
        // ─────────────────────────────────────────────────────────────────────

        // ── Test 1: Cold removes Burn before building Cold ────────────────────

        [Fact]
        public void ThermalSystem_ApplyCold_RemovesBurnFirstBeforeBuildingCold()
        {
            // currentBurn = 20, coldPower = 30 → burnRemoved = 20, leftover = 10 → coldBuilt = 10
            ThermalSystem.ApplyCold(
                coldPower: 30, coldResistance: 0,
                currentBurnBar: 20, currentColdBar: 0,
                out int newBurn, out int newCold);

            Assert.Equal(0, newBurn);   // burn fully removed (20 of 20)
            Assert.Equal(10, newCold);  // 30 - 20 = 10 leftover → cold bar
        }

        [Fact]
        public void ThermalSystem_ApplyCold_WhenBurnExceedsPower_NoColdBuilt()
        {
            // currentBurn = 50, coldPower = 30 → burnRemoved = 30, leftover = 0 → coldBuilt = 0
            ThermalSystem.ApplyCold(
                coldPower: 30, coldResistance: 0,
                currentBurnBar: 50, currentColdBar: 0,
                out int newBurn, out int newCold);

            Assert.Equal(20, newBurn);  // only 30 removed from 50
            Assert.Equal(0, newCold);   // no leftover → no cold buildup
        }

        // ── Test 2: Fire removes Cold before building Burn ────────────────────

        [Fact]
        public void ThermalSystem_ApplyFire_RemovesColdFirstBeforeBuildingBurn()
        {
            // currentCold = 20, firePower = 30 → coldRemoved = 20, leftover = 10 → burnBuilt = 10
            ThermalSystem.ApplyFire(
                firePower: 30, fireResistance: 0,
                currentColdBar: 20, currentBurnBar: 0,
                out int newCold, out int newBurn);

            Assert.Equal(0, newCold);   // cold fully removed
            Assert.Equal(10, newBurn);  // 30 - 20 = 10 leftover → burn bar
        }

        [Fact]
        public void ThermalSystem_ApplyFire_WhenColdExceedsPower_NoBurnBuilt()
        {
            // currentCold = 50, firePower = 30 → coldRemoved = 30, leftover = 0 → burnBuilt = 0
            ThermalSystem.ApplyFire(
                firePower: 30, fireResistance: 0,
                currentColdBar: 50, currentBurnBar: 0,
                out int newCold, out int newBurn);

            Assert.Equal(20, newCold);  // only 30 removed from 50
            Assert.Equal(0, newBurn);   // no leftover
        }

        // ── Test 3: Opposite-bar removal ignores resistance ──────────────────

        [Fact]
        public void ThermalSystem_ApplyCold_BurnRemovalIgnoresColdResistance()
        {
            // Even with 100% cold resistance, burn bar removal still happens in full.
            ThermalSystem.ApplyCold(
                coldPower: 30, coldResistance: 100,
                currentBurnBar: 20, currentColdBar: 0,
                out int newBurn, out int newCold);

            // Burn removal ignores resistance → all 20 burn removed
            Assert.Equal(0, newBurn);
            // Leftover = 10, but cold resistance = 100% → coldBuilt = 0
            Assert.Equal(0, newCold);
        }

        [Fact]
        public void ThermalSystem_ApplyFire_ColdRemovalIgnoresFireResistance()
        {
            // Even with 100% fire resistance, cold bar removal still happens in full.
            ThermalSystem.ApplyFire(
                firePower: 30, fireResistance: 100,
                currentColdBar: 20, currentBurnBar: 0,
                out int newCold, out int newBurn);

            Assert.Equal(0, newCold);  // cold fully removed despite 100% fire resistance
            Assert.Equal(0, newBurn);  // leftover = 10, but fireResistance=100 → no burn buildup
        }

        // ── Test 4: Cold resistance reduces Cold buildup only ─────────────────

        [Fact]
        public void ThermalSystem_ApplyCold_ColdResistanceReducesColdBuildup()
        {
            // No burn bar. coldPower = 40, resistance = 50% → coldBuilt = 20
            ThermalSystem.ApplyCold(
                coldPower: 40, coldResistance: 50,
                currentBurnBar: 0, currentColdBar: 0,
                out int newBurn, out int newCold);

            Assert.Equal(0, newBurn);
            Assert.Equal(20, newCold);  // 40 * 0.5 = 20
        }

        [Fact]
        public void ThermalSystem_ApplyCold_ColdResistanceDoesNotAffectBurnRemoval()
        {
            // Burn = 15, coldPower = 30, coldResistance = 100% (capped at 90%).
            // Burn removal: 15 removed (ignores resistance). Leftover = 15.
            // Cold buildup: (int)(15 * 0.0999...) = 1 — cap means 10% always gets through.
            ThermalSystem.ApplyCold(
                coldPower: 30, coldResistance: 100,
                currentBurnBar: 15, currentColdBar: 0,
                out int newBurn, out int newCold);

            Assert.Equal(0, newBurn);  // burn removed regardless of resistance
            Assert.Equal(1, newCold);  // 10% of leftover still builds due to 90% cap
        }

        // ── Test 5: Fire resistance reduces Burn buildup only ─────────────────

        [Fact]
        public void ThermalSystem_ApplyFire_FireResistanceReducesBurnBuildup()
        {
            // No cold bar. firePower = 40, fireResistance = 50% → burnBuilt = 20
            ThermalSystem.ApplyFire(
                firePower: 40, fireResistance: 50,
                currentColdBar: 0, currentBurnBar: 0,
                out int newCold, out int newBurn);

            Assert.Equal(0, newCold);
            Assert.Equal(20, newBurn);  // 40 * 0.5 = 20
        }

        [Fact]
        public void ThermalSystem_ApplyFire_FireResistanceDoesNotAffectColdRemoval()
        {
            // Cold = 15, firePower = 30, fireResistance = 100% (capped at 90%).
            // Cold removal: 15 removed (ignores resistance). Leftover = 15.
            // Burn buildup: (int)(15 * 0.0999...) = 1 — cap means 10% always gets through.
            ThermalSystem.ApplyFire(
                firePower: 30, fireResistance: 100,
                currentColdBar: 15, currentBurnBar: 0,
                out int newCold, out int newBurn);

            Assert.Equal(0, newCold);  // cold removed regardless of fire resistance
            Assert.Equal(1, newBurn);  // 10% of leftover still builds due to 90% cap
        }

        // ── Test 6: Cold >= 50 adds "slow" ───────────────────────────────────

        [Fact]
        public void ThermalSystem_GetStatusEffects_Cold50_AddsSlow()
        {
            var effects = ThermalSystem.GetThermalStatusEffects(coldBar: 50, burnBar: 0, isFrozen: false);
            Assert.Contains(ThermalSystem.StatusSlow, effects);
            Assert.DoesNotContain(ThermalSystem.StatusFrozen, effects);
        }

        [Fact]
        public void ThermalSystem_GetStatusEffects_ColdBelow50_NoSlow()
        {
            var effects = ThermalSystem.GetThermalStatusEffects(coldBar: 49, burnBar: 0, isFrozen: false);
            Assert.DoesNotContain(ThermalSystem.StatusSlow, effects);
        }

        // ── Test 7: Burn >= 50 adds "burning" ────────────────────────────────

        [Fact]
        public void ThermalSystem_GetStatusEffects_Burn50_AddsBurning()
        {
            var effects = ThermalSystem.GetThermalStatusEffects(coldBar: 0, burnBar: 50, isFrozen: false);
            Assert.Contains(ThermalSystem.StatusBurning, effects);
        }

        [Fact]
        public void ThermalSystem_GetStatusEffects_BurnBelow50_NoBurning()
        {
            var effects = ThermalSystem.GetThermalStatusEffects(coldBar: 0, burnBar: 49, isFrozen: false);
            Assert.DoesNotContain(ThermalSystem.StatusBurning, effects);
        }

        // ── Test 9: Freeze retains Cold bar at 40 ────────────────────────────

        [Fact]
        public void ThermalSystem_CheckFreezeTriggered_AtMax_ReducesBarTo40()
        {
            int cold = 100;
            bool triggered = ThermalSystem.CheckFreezeTriggered(ref cold);
            Assert.True(triggered);
            Assert.Equal(ThermalSystem.FrozenRetainedBar, cold);
        }

        [Fact]
        public void ThermalSystem_CheckFreezeTriggered_Below100_ReturnsFalse()
        {
            int cold = 99;
            bool triggered = ThermalSystem.CheckFreezeTriggered(ref cold);
            Assert.False(triggered);
            Assert.Equal(99, cold);
        }

        // ── Test 10 & 11: Burning DOT ─────────────────────────────────────────

        [Fact]
        public void ThermalSystem_ComputeBurnDot_At50_IsCorrect()
        {
            int dot = ThermalSystem.ComputeBurnDot(50);
            Assert.Equal((int)(50 * ThermalSystem.BurnDotPerBarPoint), dot);
            Assert.True(dot > 0);
        }

        [Fact]
        public void ThermalSystem_ComputeBurnDot_ScalesWithBar()
        {
            int dotLow = ThermalSystem.ComputeBurnDot(50);
            int dotHigh = ThermalSystem.ComputeBurnDot(100);
            Assert.True(dotHigh > dotLow, $"DOT at 100 ({dotHigh}) should exceed DOT at 50 ({dotLow})");
        }

        // ── Slow: hit count reduction ─────────────────────────────────────────

        [Fact]
        public void ThermalSystem_ResolveAgiHits_NoSlow_MatchesHitCount()
        {
            // Agi = 120 → HitCount = 1 + 120/100 = 2. No slow → same.
            int hits = ThermalSystem.ResolveAgiHits(agi: 120, isSlow: false);
            Assert.Equal(2, hits);
        }

        [Fact]
        public void ThermalSystem_ResolveAgiHits_SlowHalvesBase_TwoHitUnitBecomesOne()
        {
            // Agi = 120: base=1, agiBonusHits=1. Slow halves base: floor(1/2)=0. Result = max(1, 0+1) = 1.
            int hits = ThermalSystem.ResolveAgiHits(agi: 120, isSlow: true);
            Assert.Equal(1, hits);
        }

        [Fact]
        public void ThermalSystem_ResolveAgiHits_SlowNeverReducesToZero()
        {
            // Agi = 50: base=1, agiBonusHits=0. Slow: floor(1/2)=0. Result = max(1, 0+0) = 1.
            int hits = ThermalSystem.ResolveAgiHits(agi: 50, isSlow: true);
            Assert.Equal(1, hits);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Integration tests via BattleSession
        // ─────────────────────────────────────────────────────────────────────

        // Helpers ─────────────────────────────────────────────────────────────

        private static SkillEffect[] ColdDamageEffect(double wisScale = 1.0, int buildupPower = 0) =>
            new SkillEffect[]
            {
                new(EffectKind.Damage, BattleSkillTarget.Enemy,
                    new DamageComponent[]
                    {
                        new(EffectType.Cold, new DamageScaling[] { new("wis", wisScale) }, buildupPower)
                    })
            };

        private static SkillEffect[] FireDamageEffect(double wisScale = 1.0, int buildupPower = 0) =>
            new SkillEffect[]
            {
                new(EffectKind.Damage, BattleSkillTarget.Enemy,
                    new DamageComponent[]
                    {
                        new(EffectType.Fire, new DamageScaling[] { new("wis", wisScale) }, buildupPower)
                    })
            };

        private static SkillEffect[] PhysicalDamageEffect(double strScale = 0.001) =>
            new SkillEffect[]
            {
                new(EffectKind.Damage, BattleSkillTarget.Enemy,
                    new DamageComponent[]
                    {
                        new(EffectType.Physical, new DamageScaling[] { new("str", strScale) })
                    })
            };

        // buildupPower=100 fills the cold bar exactly, triggering freeze.
        private static BattleSkill MassiveColdSkill(string id = "massive-cold") =>
            new(id, "Massive Cold", Cost: 0, TotalDamageMultiplier: 1.0,
                Effects: ColdDamageEffect(wisScale: 1.0, buildupPower: 100));

        private static BattleSkill MassiveFireSkill(string id = "massive-fire") =>
            new(id, "Massive Fire", Cost: 0, TotalDamageMultiplier: 1.0,
                Effects: FireDamageEffect(wisScale: 1.0, buildupPower: 100));

        private static BattleSkill TinyPhysicalSkill(string id = "tiny") =>
            new(id, "Tiny", Cost: 0, TotalDamageMultiplier: 1.0,
                Effects: PhysicalDamageEffect(strScale: 0.001));

        // Player always goes first (high WIS/AGI/HP); enemy is tanky (str=10000) with configurable resistances.
        private static BattleSession BuildSession(
            BattleSkill playerSkill,
            BattleSkill enemySkill,
            IReadOnlyDictionary<EffectType, int>? enemyResistances = null,
            int enemyStr = 10000,  // 10000 * 100 = 1 000 000 HP — survives massive hits
            int playerWis = 50,
            int playerAgi = 200,   // ensures player goes first always
            int seed = 42,
            int enemyThermalProtection = 0)
        {
            var player = new BattleUnit("player", "Player", "player",
                Level: 1, Str: 10, Wis: playerWis, Agi: playerAgi,
                Skills: new BattleSkill[] { playerSkill },
                Resistances: null);

            var enemy = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: enemyStr, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { enemySkill },
                Resistances: enemyResistances,
                ThermalProtection: enemyThermalProtection);

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });
            return session;
        }

        // ── Test 14: Thermal bars are present in runtime state ────────────────

        [Fact]
        public void ThermalBars_PresentInUnitStateFromStart()
        {
            var session = BuildSession(TinyPhysicalSkill(), TinyPhysicalSkill());
            var view = session.GetView();
            foreach (var unit in view.Units)
            {
                var bars = unit.Bars;
                Assert.NotNull(bars);
                Assert.True(bars!.ContainsKey(ThermalSystem.BarCold),
                    $"Unit {unit.UnitId} is missing the '{ThermalSystem.BarCold}' bar");
                Assert.True(bars.ContainsKey(ThermalSystem.BarBurn),
                    $"Unit {unit.UnitId} is missing the '{ThermalSystem.BarBurn}' bar");
                Assert.Equal(0, bars[ThermalSystem.BarCold]);
                Assert.Equal(0, bars[ThermalSystem.BarBurn]);
            }
        }

        [Fact]
        public void ThermalBars_PresentInBattleUnitMaxBars()
        {
            var unit = new BattleUnit("u", "U", "player", Level: 1, Str: 10, Wis: 10, Agi: 50);
            Assert.True(unit.MaxBars.ContainsKey(ThermalSystem.BarCold));
            Assert.True(unit.MaxBars.ContainsKey(ThermalSystem.BarBurn));
            Assert.Equal(ThermalSystem.MaxBar, unit.MaxBars[ThermalSystem.BarCold]);
            Assert.Equal(ThermalSystem.MaxBar, unit.MaxBars[ThermalSystem.BarBurn]);
        }

        // ── Test 6 (integration): Cold >= 50 → "slow" in StatusEffects ───────

        [Fact]
        public void Integration_ColdBar50_AppliesSlowStatus()
        {
            // buildupPower: 60 → cold bar = 60 after hit (no resistance, no variance).
            // AdvanceTurnCommand processes only the player's turn so no enemy decay applies yet.
            var coldSkill = new BattleSkill("cold-med", "Cold", Cost: 0, TotalDamageMultiplier: 1.0,
                Effects: ColdDamageEffect(wisScale: 1.0, buildupPower: 60));

            // playerAgi: 50 → HitCount = 1 (single hit), still goes before enemy (agi=1).
            var session = BuildSession(coldSkill, TinyPhysicalSkill(), playerWis: 10, playerAgi: 50, seed: 10);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            int coldBar = enemyState.GetBar(ThermalSystem.BarCold);

            Assert.True(coldBar >= ThermalSystem.SlowThreshold && coldBar < ThermalSystem.FrozenThreshold,
                $"Cold bar should be in the slow range [50,100), but was {coldBar}");
            Assert.NotNull(enemyState.StatusEffects);
            Assert.Contains(ThermalSystem.StatusSlow, enemyState.StatusEffects!);
            Assert.DoesNotContain(ThermalSystem.StatusFrozen, enemyState.StatusEffects!);
        }

        // ── Test 7 (integration): Burn >= 50 → "burning" in StatusEffects ────

        [Fact]
        public void Integration_MassiveFire_AppliesBurningStatus()
        {
            // Use AdvanceTurnCommand: process the player's fire turn, then read state.
            // (PlayerActionCommand auto-advances enemy turn which includes decay; state is still valid.)
            var session = BuildSession(MassiveFireSkill(), TinyPhysicalSkill(), seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyState.StatusEffects);
            Assert.Contains(ThermalSystem.StatusBurning, enemyState.StatusEffects!);
        }

        // ── Test 8 (integration): Freeze — enemy loses exactly 1 turn ─────────

        [Fact]
        public void Integration_MassiveCold_FreezesEnemy_LosesOneTurn()
        {
            // Use AdvanceTurnCommand (no AutoAdvance) so we can observe state between turns.
            // Player (agi=200) always goes before enemy (agi=1).
            var session = BuildSession(MassiveColdSkill(), TinyPhysicalSkill(), seed: 42);

            // AdvanceTurn 1 → player attacks with massive cold → enemy freezes.
            var r1 = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(r1.Accepted);

            // After player's turn: enemy should be marked frozen in the state.
            var enemyStateAfterFreeze = r1.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyStateAfterFreeze.StatusEffects);
            Assert.Contains(ThermalSystem.StatusFrozen, enemyStateAfterFreeze.StatusEffects!);

            // AdvanceTurn 2 → enemy's frozen turn is consumed (action skipped).
            var r2 = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(r2.Accepted);

            // A "cannot act" event must appear from the enemy's skipped turn.
            var frozenSkipEvent = r2.Events.FirstOrDefault(e =>
                e.ActorId == "enemy" && e.Description.Contains("cannot act"));
            Assert.NotNull(frozenSkipEvent);

            // After the frozen turn is consumed, the frozen status is gone.
            var enemyStateAfterTurn = r2.View.Units.First(u => u.UnitId == "enemy");
            bool stillFrozen = enemyStateAfterTurn.StatusEffects?.Contains(ThermalSystem.StatusFrozen) ?? false;
            Assert.False(stillFrozen, "Frozen status should be consumed after exactly 1 skipped turn");
        }

        // ── Test 9 (integration): Freeze retains Cold bar at 40 ───────────────

        [Fact]
        public void Integration_MassiveCold_FreezeSetsBarTo40()
        {
            var session = BuildSession(MassiveColdSkill(), TinyPhysicalSkill(), seed: 42);

            // Player attacks with massive cold → freeze triggers and bar is reduced to 40.
            // Using AdvanceTurnCommand so we see the state before the frozen turn is consumed.
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            int coldBar = enemyState.GetBar(ThermalSystem.BarCold);

            // Bar must be exactly FrozenRetainedBar — the freeze trigger reduces it.
            Assert.Equal(ThermalSystem.FrozenRetainedBar, coldBar);
        }

        // ── Test 10 & 11 (integration): Burning DOT and scaling ───────────────

        [Fact]
        public void Integration_BurningDot_DealsDamageAtStartOfTurn()
        {
            // Player fires up enemy, then enemy's own start-of-turn applies burn DOT.
            // Using AdvanceTurnCommand to observe each turn individually.
            // enemyStr=10000 → MaxHP=1,000,000; massive fire (~400k damage) does not kill it.
            var session = BuildSession(MassiveFireSkill(), TinyPhysicalSkill(), seed: 42);

            // AdvanceTurn 1 (player): apply massive fire → enemy burn bar = 100, burning.
            session.TryExecute(new AdvanceTurnCommand());

            // Read enemy HP after receiving fire damage but BEFORE their turn.
            int enemyHpAfterFire = session.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            Assert.True(enemyHpAfterFire > 0, "Enemy should survive the massive fire hit");

            // AdvanceTurn 2 (enemy): start-of-turn burn DOT reduces enemy HP first.
            var r = session.TryExecute(new AdvanceTurnCommand());

            int enemyHpAfterDot = r.View.Units.First(u => u.UnitId == "enemy").CurrentHp;

            Assert.True(enemyHpAfterDot < enemyHpAfterFire,
                $"Enemy HP should decrease from burn DOT (was {enemyHpAfterFire}, now {enemyHpAfterDot})");

            // The burn DOT event should be present in this step's events.
            Assert.Contains(r.Events, e =>
                e.ActorId == "enemy" && e.Type == "status" && e.Value > 0);
        }

        [Fact]
        public void Integration_BurningDot_ScalesWithBurnBar()
        {
            // Low burn bar (~50): DOT from ThermalSystem.ComputeBurnDot(50)
            int dotAtHalf = ThermalSystem.ComputeBurnDot(50);
            // High burn bar (100): DOT from ThermalSystem.ComputeBurnDot(100)
            int dotAtFull = ThermalSystem.ComputeBurnDot(100);

            Assert.True(dotAtFull > dotAtHalf,
                $"Burn DOT at 100 ({dotAtFull}) should exceed DOT at 50 ({dotAtHalf})");
        }

        // ── Test 12: Fire thaws before building Burn ──────────────────────────

        [Fact]
        public void Integration_Fire_ThawsNearlyFrozenTarget_BeforeCreatingBurn()
        {
            // Verify opposition math directly: cold=70, firePower=80 → cold fully thawed, 10 burn built.
            ThermalSystem.ApplyFire(
                firePower: 80, fireResistance: 0,
                currentColdBar: 70, currentBurnBar: 0,
                out int newCold, out int newBurn);

            Assert.Equal(0, newCold);  // cold fully thawed
            Assert.Equal(10, newBurn); // leftover creates burn
        }

        // ── Test 13: Cold extinguishes before building Cold ───────────────────

        [Fact]
        public void Integration_Cold_ExtinguishesBurning_BeforeCreatingCold()
        {
            // If burn = 70, cold power = 80:
            // burnRemoved = 70, newBurn = 0
            // leftover = 10, resistance = 0 → coldBuilt = 10
            ThermalSystem.ApplyCold(
                coldPower: 80, coldResistance: 0,
                currentBurnBar: 70, currentColdBar: 0,
                out int newBurn, out int newCold);

            Assert.Equal(0, newBurn);  // burn fully extinguished
            Assert.Equal(10, newCold); // leftover creates cold
        }

        // ── Test 15 (integration): StatusEffects contains correct IDs ─────────

        [Fact]
        public void Integration_NoThermal_StatusEffectsIsNullOrEmpty()
        {
            var session = BuildSession(TinyPhysicalSkill(), TinyPhysicalSkill(), seed: 42);
            var view = session.GetView();
            foreach (var unit in view.Units)
            {
                // At battle start, no thermal buildup → no status effects.
                bool hasNone = unit.StatusEffects == null || unit.StatusEffects.Count == 0;
                Assert.True(hasNone, $"Unit {unit.UnitId} should have no status effects at battle start");
            }
        }

        [Fact]
        public void Integration_MassiveFire_StatusEffectsContainsBurning()
        {
            var session = BuildSession(MassiveFireSkill(), TinyPhysicalSkill(), seed: 42);
            // AdvanceTurnCommand: process player's fire turn without auto-advancing to enemy.
            var result = session.TryExecute(new AdvanceTurnCommand());
            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyState.StatusEffects);
            Assert.Contains(ThermalSystem.StatusBurning, enemyState.StatusEffects!);
        }

        [Fact]
        public void Integration_MassiveCold_StatusEffectsContainsFrozen()
        {
            var session = BuildSession(MassiveColdSkill(), TinyPhysicalSkill(), seed: 42);

            // Use AdvanceTurnCommand so frozen state is visible before AutoAdvance consumes it.
            var result = session.TryExecute(new AdvanceTurnCommand());

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyState.StatusEffects);
            Assert.Contains(ThermalSystem.StatusFrozen, enemyState.StatusEffects!);
        }

        // ── Test 16: Session path and full-run path stay aligned ──────────────

        [Fact]
        public void Integration_SessionAndFullRun_ProduceSameWinner()
        {
            // Both the session-based path (AdvanceTurn) and BattleEngine.RunFull should agree.
            var scenario = new SampleScenario();
            var setup = scenario.CreateSetup(TestContentSource.Default);

            var engineResult = BattleEngine.Run(setup, scenario.Seed);

            var session = new BattleSession(scenario.Seed);
            session.Start(setup);
            BattleStepResult last = null!;
            for (int i = 0; i < 1000 && !session.GetView().IsOver; i++)
                last = session.TryExecute(new AdvanceTurnCommand());

            Assert.Equal(engineResult.WinningTeam, last.View.WinningTeam);
        }

        // ── Test 17: Determinism with the same seed ───────────────────────────

        [Fact]
        public void Integration_SameSeed_ProducesDeterministicResult()
        {
            var scenario = new SampleScenario();
            var setup = scenario.CreateSetup(TestContentSource.Default);

            static string RunAndGetWinner(BattleSetup setup, int seed)
            {
                var session = new BattleSession(seed);
                session.Start(setup);
                for (int i = 0; i < 1000 && !session.GetView().IsOver; i++)
                    session.TryExecute(new AdvanceTurnCommand());
                return session.GetView().WinningTeam ?? "";
            }

            string a = RunAndGetWinner(setup, scenario.Seed);
            string b = RunAndGetWinner(setup, scenario.Seed);

            Assert.Equal(a, b);
        }

        [Fact]
        public void Integration_SameSeed_FullLogLengthIsDeterministic()
        {
            var scenario = new SampleScenario();
            var setup = scenario.CreateSetup(TestContentSource.Default);

            static int RunAndGetLogCount(BattleSetup setup, int seed)
            {
                var session = new BattleSession(seed);
                session.Start(setup);
                for (int i = 0; i < 1000 && !session.GetView().IsOver; i++)
                    session.TryExecute(new AdvanceTurnCommand());
                return session.GetView().FullLog.Count;
            }

            Assert.Equal(RunAndGetLogCount(setup, scenario.Seed), RunAndGetLogCount(setup, scenario.Seed));
        }

        // ── Thermal bar cap: never exceeds MaxBar ─────────────────────────────

        [Fact]
        public void ThermalSystem_ApplyCold_CapsAtMaxBar()
        {
            // currentColdBar = 90, coldPower = 50, no burn, no resistance
            // leftover = 50 → coldBuilt should cap at MaxBar (100)
            ThermalSystem.ApplyCold(
                coldPower: 50, coldResistance: 0,
                currentBurnBar: 0, currentColdBar: 90,
                out _, out int newCold);

            Assert.Equal(ThermalSystem.MaxBar, newCold);
        }

        [Fact]
        public void ThermalSystem_ApplyFire_CapsAtMaxBar()
        {
            ThermalSystem.ApplyFire(
                firePower: 50, fireResistance: 0,
                currentColdBar: 0, currentBurnBar: 90,
                out _, out int newBurn);

            Assert.Equal(ThermalSystem.MaxBar, newBurn);
        }

        // ── Thermal decay ─────────────────────────────────────────────────────

        [Fact]
        public void ThermalSystem_Decay_ReducesBothBars()
        {
            var (newCold, newBurn) = ThermalSystem.ApplyDecay(coldBar: 60, burnBar: 70);
            Assert.Equal(60 - ThermalSystem.ColdDecayPerTurn, newCold);
            Assert.Equal(70 - ThermalSystem.BurnDecayPerTurn, newBurn);
        }

        [Fact]
        public void ThermalSystem_Decay_NeverGoesNegative()
        {
            var (newCold, newBurn) = ThermalSystem.ApplyDecay(coldBar: 5, burnBar: 3);
            Assert.Equal(0, newCold);
            Assert.Equal(0, newBurn);
        }

        // ── Frozen and burning are independent ───────────────────────────────

        [Fact]
        public void ThermalSystem_FrozenAndBurning_CanCoexist()
        {
            // A unit can be frozen (cold bar was 100) AND have high burn bar.
            // frozen takes priority over slow in the status list.
            // burning is independent.
            var effects = ThermalSystem.GetThermalStatusEffects(
                coldBar: ThermalSystem.FrozenRetainedBar, burnBar: 80, isFrozen: true);

            Assert.Contains(ThermalSystem.StatusFrozen, effects);
            Assert.Contains(ThermalSystem.StatusBurning, effects);
            Assert.DoesNotContain(ThermalSystem.StatusSlow, effects);
        }

        [Fact]
        public void ThermalSystem_FrozenTakesPriorityOverSlow()
        {
            // When isFrozen=true, "slow" should not appear even if cold bar ≥ 50.
            var effects = ThermalSystem.GetThermalStatusEffects(
                coldBar: 60, burnBar: 0, isFrozen: true);

            Assert.Contains(ThermalSystem.StatusFrozen, effects);
            Assert.DoesNotContain(ThermalSystem.StatusSlow, effects);
        }

        // ── Snapshot carries thermal bars ────────────────────────────────────

        [Fact]
        public void Integration_Snapshot_ContainsThermalBars()
        {
            var scenario = new SampleScenario();
            var setup = scenario.CreateSetup(TestContentSource.Default);
            var result = BattleEngine.Run(setup, scenario.Seed);

            // Every snapshot must have cold and burn bars for every unit.
            foreach (var snap in result.Snapshots)
            {
                foreach (var state in snap.UnitStates)
                {
                    Assert.NotNull(state.Bars);
                    Assert.True(state.Bars!.ContainsKey(ThermalSystem.BarCold),
                        $"Snapshot step {snap.Step} unit {state.UnitId} missing '{ThermalSystem.BarCold}' bar");
                    Assert.True(state.Bars.ContainsKey(ThermalSystem.BarBurn),
                        $"Snapshot step {snap.Step} unit {state.UnitId} missing '{ThermalSystem.BarBurn}' bar");
                }
            }
        }

        // ── Test 18: ThermalProtection amplifies resistance for buildup ────────

        [Fact]
        public void ThermalProtection_AmplifiesColdResistanceForBuildup()
        {
            // base cold resistance = 50 (half buildup). With +20% thermal protection → 60% effective.
            var coldResistances = new Dictionary<EffectType, int> { [EffectType.Cold] = 50 };

            var sessionNoProtection = BuildSession(
                MassiveColdSkill(), TinyPhysicalSkill(),
                enemyResistances: coldResistances,
                enemyStr: 10000, playerWis: 5, playerAgi: 1, seed: 10);
            var r0 = sessionNoProtection.TryExecute(new AdvanceTurnCommand());
            int coldBarNoProtection = r0.View.Units.First(u => u.UnitId == "enemy").GetBar(ThermalSystem.BarCold);

            var sessionWithProtection = BuildSession(
                MassiveColdSkill(), TinyPhysicalSkill(),
                enemyResistances: coldResistances,
                enemyStr: 10000, playerWis: 5, playerAgi: 1, seed: 10,
                enemyThermalProtection: 20);
            var r1 = sessionWithProtection.TryExecute(new AdvanceTurnCommand());
            int coldBarWithProtection = r1.View.Units.First(u => u.UnitId == "enemy").GetBar(ThermalSystem.BarCold);

            Assert.True(coldBarWithProtection < coldBarNoProtection,
                $"ThermalProtection should reduce cold buildup (no-protection={coldBarNoProtection}, with-protection={coldBarWithProtection})");
        }

        [Fact]
        public void ThermalProtection_AmplifiesFireResistanceForBuildup()
        {
            // base fire resistance = 50 (half buildup). With +20% thermal protection → 60% effective.
            var fireResistances = new Dictionary<EffectType, int> { [EffectType.Fire] = 50 };

            var sessionNoProtection = BuildSession(
                MassiveFireSkill(), TinyPhysicalSkill(),
                enemyResistances: fireResistances,
                enemyStr: 10000, playerWis: 5, playerAgi: 99, seed: 10);
            var r0 = sessionNoProtection.TryExecute(new AdvanceTurnCommand());
            int burnBarNoProtection = r0.View.Units.First(u => u.UnitId == "enemy").GetBar(ThermalSystem.BarBurn);

            var sessionWithProtection = BuildSession(
                MassiveFireSkill(), TinyPhysicalSkill(),
                enemyResistances: fireResistances,
                enemyStr: 10000, playerWis: 5, playerAgi: 99, seed: 10,
                enemyThermalProtection: 20);
            var r1 = sessionWithProtection.TryExecute(new AdvanceTurnCommand());
            int burnBarWithProtection = r1.View.Units.First(u => u.UnitId == "enemy").GetBar(ThermalSystem.BarBurn);

            Assert.True(burnBarWithProtection < burnBarNoProtection,
                $"ThermalProtection should reduce burn buildup (no-protection={burnBarNoProtection}, with-protection={burnBarWithProtection})");
        }

        [Fact]
        public void ThermalProtection_DoesNotAffectOpposingBarRemoval()
        {
            // Cold removes burn bar first, ignoring all resistance — thermal protection must not change this.
            // coldPower=50, boostedColdResistance=100 (→ capped at 90 inside ThermalSystem), currentBurnBar=30.
            // Burn removal = min(30, 50) = 30 (ignores resistance).
            // Leftover = 20, factor = 1.0 - 0.90 = 0.10 → coldBuilt = int(20 * 0.10) = 2.
            int boostedColdResistance = (int)(50 * (1.0 + 100 / 100.0)); // 100, capped to 90 inside
            ThermalSystem.ApplyCold(
                coldPower: 50, coldResistance: boostedColdResistance,
                currentBurnBar: 30, currentColdBar: 0,
                out int newBurn, out int newCold);

            Assert.Equal(0, newBurn);  // all burn removed (resistance ignored for bar removal)
            Assert.Equal(1, newCold);  // 20 leftover * ~0.10 (90% cap, float truncation) = 1
        }

        [Fact]
        public void ThermalProtection_OnlyAffectsBuildup_NotDamageTaken()
        {
            // ThermalProtection must NOT reduce fire/cold damage taken — only buildup accumulation.
            // Use a cold skill that deals damage and also builds the cold bar.
            // HP loss should be identical with or without thermal protection.
            var coldSkill = new BattleSkill("cold-small", "Cold", Cost: 0, TotalDamageMultiplier: 1.0,
                Effects: ColdDamageEffect(wisScale: 0.1, buildupPower: 60));

            var sessionNoProtection = BuildSession(
                coldSkill, TinyPhysicalSkill(),
                enemyStr: 10000, playerWis: 50, playerAgi: 200, seed: 10);
            var r0 = sessionNoProtection.TryExecute(new AdvanceTurnCommand());
            int hpNoProtection = r0.View.Units.First(u => u.UnitId == "enemy").CurrentHp;

            var sessionWithProtection = BuildSession(
                coldSkill, TinyPhysicalSkill(),
                enemyStr: 10000, playerWis: 50, playerAgi: 200, seed: 10,
                enemyThermalProtection: 50);
            var r1 = sessionWithProtection.TryExecute(new AdvanceTurnCommand());
            int hpWithProtection = r1.View.Units.First(u => u.UnitId == "enemy").CurrentHp;

            Assert.Equal(hpNoProtection, hpWithProtection);
        }
    }
}
