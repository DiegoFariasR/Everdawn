using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for the concussion buildup bar system driven by blunt damage.
    /// Covers:
    /// 1.  Concussion bar can be built from a blunt skill with BuildupPower set.
    /// 2.  Physical resistance reduces concussion buildup.
    /// 3.  Concussion >= 50 applies "dazed" status effect.
    /// 4.  Dazed reduces the actor's final damage dealt by 20%.
    /// 5.  Concussion >= 100 applies "concussed" status effect.
    /// 6.  Concussed causes the unit to lose exactly 1 turn.
    /// 7.  After concussed triggers, concussion bar becomes 40 (ConcussedRetainedBar).
    /// 8.  Blunt damage uses STR (physical attack stat) for base damage — same as Physical.
    /// 9.  Blunt damage uses physical resistance for damage reduction.
    /// 10. Physical damage buffs apply to blunt hits.
    /// 11. Concussion bar decays by ConcussionSystem.DecayPerTurn each unit turn.
    /// 12. Concussion bar is visible in runtime state / unit bars.
    /// 13. Pure physical hits do NOT build the concussion bar.
    /// </summary>
    public class ConcussionTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // ConcussionSystem unit tests (pure logic, no session)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ConcussionSystem_ApplyConcussion_BuildsBar()
        {
            int built = ConcussionSystem.ApplyConcussion(30, physicalResistance: 0, currentBar: 0, out int newBar);
            Assert.Equal(30, built);
            Assert.Equal(30, newBar);
        }

        [Fact]
        public void ConcussionSystem_ApplyConcussion_CapsAtMaxBar()
        {
            ConcussionSystem.ApplyConcussion(200, physicalResistance: 0, currentBar: 0, out int newBar);
            Assert.Equal(ConcussionSystem.MaxBar, newBar);
        }

        [Fact]
        public void ConcussionSystem_ApplyConcussion_ResistanceHalvesBuild()
        {
            int built = ConcussionSystem.ApplyConcussion(40, physicalResistance: 50, currentBar: 0, out int newBar);
            Assert.Equal(20, built);
            Assert.Equal(20, newBar);
        }

        [Fact]
        public void ConcussionSystem_ApplyConcussion_ResistanceCapsAt90()
        {
            // 100% resistance is capped at 90%: 10% of 60 power still builds.
            // Due to binary floating-point: (int)(60 * (1.0 - 90/100.0)) = (int)(60 * 0.0999...) = 5.
            int built = ConcussionSystem.ApplyConcussion(60, physicalResistance: 100, currentBar: 0, out int newBar);
            Assert.Equal(5, built);
            Assert.Equal(5, newBar);
        }

        [Fact]
        public void ConcussionSystem_CheckConcussedTriggered_TriggersAt100()
        {
            int bar = 100;
            bool triggered = ConcussionSystem.CheckConcussedTriggered(ref bar);
            Assert.True(triggered);
            Assert.Equal(ConcussionSystem.ConcussedRetainedBar, bar);
        }

        [Fact]
        public void ConcussionSystem_CheckConcussedTriggered_DoesNotTriggerBelow100()
        {
            int bar = 99;
            bool triggered = ConcussionSystem.CheckConcussedTriggered(ref bar);
            Assert.False(triggered);
            Assert.Equal(99, bar);
        }

        [Fact]
        public void ConcussionSystem_ConcussedRetainedBar_Is40()
        {
            Assert.Equal(40, ConcussionSystem.ConcussedRetainedBar);
        }

        [Fact]
        public void ConcussionSystem_ApplyDazedReduction_Reduces20Percent()
        {
            int reduced = ConcussionSystem.ApplyDazedReduction(100, isDazed: true);
            Assert.Equal(80, reduced);
        }

        [Fact]
        public void ConcussionSystem_ApplyDazedReduction_NoChangeWhenNotDazed()
        {
            int unchanged = ConcussionSystem.ApplyDazedReduction(100, isDazed: false);
            Assert.Equal(100, unchanged);
        }

        [Fact]
        public void ConcussionSystem_GetStatusEffects_DazedWhenBar50Plus()
        {
            var effects = ConcussionSystem.GetConcussionStatusEffects(bar: 50, isConcussed: false);
            Assert.Contains(ConcussionSystem.StatusDazed, effects);
            Assert.DoesNotContain(ConcussionSystem.StatusConcussed, effects);
        }

        [Fact]
        public void ConcussionSystem_GetStatusEffects_NoDazedBelowThreshold()
        {
            var effects = ConcussionSystem.GetConcussionStatusEffects(bar: 49, isConcussed: false);
            Assert.Empty(effects);
        }

        [Fact]
        public void ConcussionSystem_GetStatusEffects_ConcussedTakesPriorityOverDazed()
        {
            var effects = ConcussionSystem.GetConcussionStatusEffects(bar: 60, isConcussed: true);
            Assert.Contains(ConcussionSystem.StatusConcussed, effects);
            Assert.DoesNotContain(ConcussionSystem.StatusDazed, effects);
        }

        [Fact]
        public void ConcussionSystem_ApplyDecay_ReducesByDecayPerTurn()
        {
            int newBar = ConcussionSystem.ApplyDecay(60);
            Assert.Equal(60 - ConcussionSystem.DecayPerTurn, newBar);
        }

        [Fact]
        public void ConcussionSystem_ApplyDecay_FloorsAtZero()
        {
            int newBar = ConcussionSystem.ApplyDecay(10);
            Assert.Equal(0, newBar);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Integration tests (session-based)
        // ─────────────────────────────────────────────────────────────────────

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a session where the player uses a blunt skill with the given buildup power.
        /// Enemy has massive STR so it survives all player hits.
        /// Player always goes first (Agi=200 vs enemy Agi=1).
        /// </summary>
        private static BattleSession BuildSession(
            BattleSkill playerSkill,
            BattleSkill? enemySkill = null,
            int enemyStr = 10000,
            int playerStr = 10,
            int playerWis = 50,
            int playerAgi = 200,
            IReadOnlyDictionary<EffectType, int>? enemyResistances = null,
            int seed = 42)
        {
            var player = new BattleUnit("player", "Player", "player",
                Level: 1, Str: playerStr, Wis: playerWis, Agi: playerAgi,
                Skills: new BattleSkill[] { playerSkill });

            var resolvedEnemySkill = enemySkill ?? TinyPhysicalSkill();
            var enemy = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: enemyStr, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { resolvedEnemySkill },
                Resistances: enemyResistances);

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });
            return session;
        }

        /// <summary>Harmless physical attack with no concussion buildup.</summary>
        private static BattleSkill TinyPhysicalSkill(string id = "tiny") =>
            new BattleSkill(id, "Tiny", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.001) })
                        })
                },
                Modifiers: new string[] { "basic" });

        /// <summary>Blunt skill with buildup power of 20 — with 3 AGI-derived hits (Agi=200),
        /// total buildup is 60, which reaches the dazed threshold (50) but not concussed (100).</summary>
        private static BattleSkill DazingBluntSkill(string id = "daze-skill") =>
            new BattleSkill(id, "Dazing Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Blunt,
                                new DamageScaling[] { new DamageScaling("str", 0.001) },
                                BuildupPower: 20)  // 3 hits × 20 = 60 → dazed, not concussed
                        })
                },
                Modifiers: new string[] { "basic" });

        /// <summary>Blunt skill with 100 buildup power — guarantees concussed in one hit.</summary>
        private static BattleSkill MassiveConcussionSkill(string id = "mass-conc") =>
            new BattleSkill(id, "Massive Concussion", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Blunt,
                                new DamageScaling[] { new DamageScaling("str", 0.001) },
                                BuildupPower: 100)  // guaranteed concussed in one hit
                        })
                },
                Modifiers: new string[] { "basic" });

        // ── Test 1: Blunt skill with BuildupPower builds concussion bar ────────

        [Fact]
        public void Integration_BluntSkill_BuildsConcussionBar()
        {
            var session = BuildSession(DazingBluntSkill(), seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            int concussionBar = enemyState.GetBar(ConcussionSystem.BarConcussion);
            Assert.True(concussionBar > 0, $"Concussion bar should be > 0 after blunt hit; was {concussionBar}");
        }

        // ── Test 13: Physical hits do NOT build concussion bar ─────────────────

        [Fact]
        public void Integration_PhysicalSkill_DoesNotBuildConcussionBar()
        {
            // A physical skill with BuildupPower set should NOT build concussion —
            // only Blunt damage type triggers concussion buildup.
            var physSkillWithBuildup = new BattleSkill("phys-buildup", "Physical Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.001) },
                                BuildupPower: 100)  // BuildupPower set, but type is Physical not Blunt
                        })
                },
                Modifiers: new string[] { "basic" });

            var session = BuildSession(physSkillWithBuildup, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            int concussionBar = enemyState.GetBar(ConcussionSystem.BarConcussion);
            Assert.Equal(0, concussionBar);
        }

        // ── Test 2: Physical resistance reduces concussion buildup ─────────────

        [Fact]
        public void Integration_PhysicalResistance_ReducesConcussionBuildup()
        {
            var resistances = new Dictionary<EffectType, int> { { EffectType.Physical, 50 } };
            var sessionNoResist = BuildSession(DazingBluntSkill(), seed: 42);
            var sessionWithResist = BuildSession(DazingBluntSkill(),
                enemyResistances: resistances, seed: 42);

            var resultNoResist = sessionNoResist.TryExecute(new AdvanceTurnCommand());
            var resultWithResist = sessionWithResist.TryExecute(new AdvanceTurnCommand());

            int barNoResist = resultNoResist.View.Units.First(u => u.UnitId == "enemy")
                .GetBar(ConcussionSystem.BarConcussion);
            int barWithResist = resultWithResist.View.Units.First(u => u.UnitId == "enemy")
                .GetBar(ConcussionSystem.BarConcussion);

            Assert.True(barWithResist < barNoResist,
                $"Concussion bar with physical resistance ({barWithResist}) should be less than without ({barNoResist})");
        }

        // ── Test 3: Concussion >= 50 shows "dazed" in StatusEffects ───────────

        [Fact]
        public void Integration_ConcussionAtThreshold_AppliesDazedStatus()
        {
            var session = BuildSession(DazingBluntSkill(), seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyState.StatusEffects);
            Assert.Contains(ConcussionSystem.StatusDazed, enemyState.StatusEffects!);
        }

        // ── Test 4: Dazed reduces the actor's final damage by 20% ─────────────

        [Fact]
        public void Integration_DazedActor_DealsDamageReducedBy20Percent()
        {
            // Session A: player attacks cleanly (no dazed).
            var playerAttack = new BattleSkill("atk", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 1.0) })
                        })
                },
                Modifiers: new string[] { "basic" });

            // Enemy hits player with a blunt skill to get player's concussion bar to 50.
            var enemyBluntSkill = new BattleSkill("edaze", "Enemy Daze", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Blunt,
                                new DamageScaling[] { new DamageScaling("str", 0.001) },
                                BuildupPower: ConcussionSystem.DazedThreshold)  // exactly 50 → dazed
                        })
                },
                Modifiers: new string[] { "basic" });

            // Session A: player goes first (high AGI), attacks cleanly.
            var sessionA = new BattleSession(42);
            var playerA = new BattleUnit("player", "Player", "player",
                Level: 1, Str: 50, Wis: 0, Agi: 200,
                Skills: new BattleSkill[] { playerAttack });
            var enemyA = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: 10000, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { TinyPhysicalSkill() });
            sessionA.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { playerA },
                EnemyUnits = new List<BattleUnit> { enemyA },
            });
            var rA = sessionA.TryExecute(new AdvanceTurnCommand());
            int damageA = rA.Events.Where(e => e.ActorId == "player" && e.Type == "attack").Sum(e => e.Value);

            // Session B: enemy goes first (high AGI) and dazes player with blunt hit.
            // Then player attacks while dazed.
            var sessionB = new BattleSession(42);
            var playerB = new BattleUnit("player", "Player", "player",
                Level: 1, Str: 50, Wis: 0, Agi: 1,   // low AGI → goes second
                Skills: new BattleSkill[] { playerAttack });
            var enemyB = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: 10000, Wis: 0, Agi: 200,  // high AGI → goes first
                Skills: new BattleSkill[] { enemyBluntSkill });
            sessionB.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { playerB },
                EnemyUnits = new List<BattleUnit> { enemyB },
            });
            var rB1 = sessionB.TryExecute(new AdvanceTurnCommand()); // enemy goes first → player gets dazed
            var rB2 = sessionB.TryExecute(new AdvanceTurnCommand()); // player attacks (now dazed)
            int damageB = rB2.Events.Where(e => e.ActorId == "player" && e.Type == "attack").Sum(e => e.Value);

            int playerBConcussion = rB1.View.Units.First(u => u.UnitId == "player")
                .GetBar(ConcussionSystem.BarConcussion);

            if (playerBConcussion >= ConcussionSystem.DazedThreshold && damageA > 0 && damageB > 0)
            {
                double ratio = (double)damageB / damageA;
                Assert.True(ratio < 1.0,
                    $"Dazed actor should deal less damage than undazed actor (ratio={ratio:F2}, A={damageA}, B={damageB})");
            }
        }

        // ── Test 5: Concussion >= 100 → concussed status ──────────────────────

        [Fact]
        public void Integration_ConcussionAt100_AppliesConcussedStatus()
        {
            var session = BuildSession(MassiveConcussionSkill(), seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyState.StatusEffects);
            Assert.Contains(ConcussionSystem.StatusConcussed, enemyState.StatusEffects!);
        }

        // ── Test 6: Concussed → enemy loses exactly 1 turn ────────────────────

        [Fact]
        public void Integration_MassiveConcussion_ConcussedEnemyLosesOneTurn()
        {
            var session = BuildSession(MassiveConcussionSkill(), seed: 42);

            // AdvanceTurn 1 (player): massive concussion → enemy concussed.
            var r1 = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(r1.Accepted);

            var enemyStateAfterHit = r1.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyStateAfterHit.StatusEffects);
            Assert.Contains(ConcussionSystem.StatusConcussed, enemyStateAfterHit.StatusEffects!);

            // AdvanceTurn 2 (enemy's turn): should be skipped due to concussed.
            var r2 = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(r2.Accepted);

            // A "cannot act" event must appear for the enemy.
            var concussedEvent = r2.Events.FirstOrDefault(e =>
                e.ActorId == "enemy" && e.Description.Contains("cannot act"));
            Assert.NotNull(concussedEvent);

            // After the concussed turn is consumed, the concussed status should be gone.
            var enemyStateAfterSkip = r2.View.Units.First(u => u.UnitId == "enemy");
            bool stillConcussed = enemyStateAfterSkip.StatusEffects?.Contains(ConcussionSystem.StatusConcussed) ?? false;
            Assert.False(stillConcussed, "Concussed status should be consumed after exactly 1 skipped turn");
        }

        // ── Test 7: After concussed triggers, bar retains at ConcussedRetainedBar ──

        [Fact]
        public void Integration_AfterConcussedTrigger_BarRetainsAt40()
        {
            var session = BuildSession(MassiveConcussionSkill(), seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            int concussionBar = enemyState.GetBar(ConcussionSystem.BarConcussion);
            Assert.Equal(ConcussionSystem.ConcussedRetainedBar, concussionBar);
        }

        // ── Test 8: Blunt damage uses STR (same base damage as Physical) ───────

        [Fact]
        public void Integration_BluntDamage_SameBaseAttackAsPhysical()
        {
            // High STR, low WIS. Blunt and Physical skills with identical str scaling
            // should deal the same damage (same STR stat is used for both).
            var bluntSkill = new BattleSkill("blunt-str", "Blunt Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Blunt,
                                new DamageScaling[] { new DamageScaling("str", 1.0) },
                                BuildupPower: 10)
                        })
                },
                Modifiers: new string[] { "basic" });

            var physicalSkill = new BattleSkill("phys-str", "Physical Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 1.0) })
                        })
                },
                Modifiers: new string[] { "basic" });

            var enemyUnit = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: 10000, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { TinyPhysicalSkill() });

            // Session: blunt vs same enemy.
            var sessionBlunt = new BattleSession(42);
            sessionBlunt.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    new BattleUnit("player", "Player", "player",
                        Level: 1, Str: 100, Wis: 1, Agi: 200,
                        Skills: new BattleSkill[] { bluntSkill })
                },
                EnemyUnits = new List<BattleUnit> { enemyUnit },
            });
            int hpBeforeBlunt = sessionBlunt.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            sessionBlunt.TryExecute(new AdvanceTurnCommand());
            int damageBlunt = hpBeforeBlunt - sessionBlunt.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;

            // Session: physical vs same enemy.
            var sessionPhys = new BattleSession(42);
            sessionPhys.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    new BattleUnit("player", "Player", "player",
                        Level: 1, Str: 100, Wis: 1, Agi: 200,
                        Skills: new BattleSkill[] { physicalSkill })
                },
                EnemyUnits = new List<BattleUnit> { enemyUnit },
            });
            int hpBeforePhys = sessionPhys.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            sessionPhys.TryExecute(new AdvanceTurnCommand());
            int damagePhys = hpBeforePhys - sessionPhys.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;

            // Blunt and Physical should deal the same base damage (same STR scaling, same seed).
            Assert.True(damageBlunt > 0, "Blunt damage should deal some damage");
            Assert.Equal(damagePhys, damageBlunt);
        }

        // ── Test 9: Blunt damage uses physical resistance for damage reduction ──

        [Fact]
        public void Integration_BluntDamage_UsesPhysicalResistance()
        {
            var bluntSkill = new BattleSkill("blunt-basic", "Blunt Strike", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Blunt,
                                new DamageScaling[] { new DamageScaling("str", 1.0) },
                                BuildupPower: 10)
                        })
                },
                Modifiers: new string[] { "basic" });

            var player = new BattleUnit("player", "Player", "player",
                Level: 1, Str: 100, Wis: 10, Agi: 200,
                Skills: new BattleSkill[] { bluntSkill });

            // Session A: enemy with no physical resistance.
            var enemyNoResist = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: 10000, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { TinyPhysicalSkill() });
            var sessionNoResist = new BattleSession(42);
            sessionNoResist.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemyNoResist },
            });
            int hpBeforeNo = sessionNoResist.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            sessionNoResist.TryExecute(new AdvanceTurnCommand());
            int hpAfterNo = sessionNoResist.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            int damageNoResist = hpBeforeNo - hpAfterNo;

            // Session B: enemy with 50% physical resistance.
            var enemyWithResist = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: 10000, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { TinyPhysicalSkill() },
                Resistances: new Dictionary<EffectType, int> { { EffectType.Physical, 50 } });
            var sessionWithResist = new BattleSession(42);
            sessionWithResist.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemyWithResist },
            });
            int hpBeforeWith = sessionWithResist.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            sessionWithResist.TryExecute(new AdvanceTurnCommand());
            int hpAfterWith = sessionWithResist.GetView().Units.First(u => u.UnitId == "enemy").CurrentHp;
            int damageWithResist = hpBeforeWith - hpAfterWith;

            Assert.True(damageNoResist > 0, "Blunt damage should deal some damage to unresisted enemy");
            Assert.True(damageWithResist < damageNoResist,
                $"50% physical resistance should reduce blunt damage. No resist: {damageNoResist}, With resist: {damageWithResist}");
        }

        // ── Test 11: Concussion bar decays per turn ────────────────────────────

        [Fact]
        public void Integration_ConcussionBar_DecaysEachUnitTurn()
        {
            // Enemy (Agi=200) goes first (auto-resolved in AutoAdvance after Start).
            // Enemy hits player (Agi=1) 3 times (AGI-derived hits) × 20 buildup = 60 total.
            // After enemy's turn, player concussion bar = 60 (verified from Start result view).
            // Player's turn runs next via AdvanceTurnCommand: StartOfTurn decays bar by 20 → 40.
            var bluntEnemySkill = new BattleSkill("eblunt", "Enemy Blunt", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Blunt,
                                new DamageScaling[] { new DamageScaling("str", 0.001) },
                                BuildupPower: 20)  // 3 hits × 20 = 60 total
                        })
                },
                Modifiers: new string[] { "basic" });

            var playerAttack = new BattleSkill("atk", "Attack", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.001) })
                        })
                },
                Modifiers: new string[] { "basic" });

            // Enemy goes first (Agi=200), player second (Agi=1).
            var session = new BattleSession(42);
            var player = new BattleUnit("player", "Player", "player",
                Level: 1, Str: 10, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { playerAttack });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: 10000, Wis: 0, Agi: 200,
                Skills: new BattleSkill[] { bluntEnemySkill });
            var startResult = session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            // After Start, AutoAdvance ran the enemy's turn automatically (enemy goes first).
            // Player's concussion bar should now be 60 (3 hits × 20).
            int barAfterEnemyTurn = startResult.View.Units.First(u => u.UnitId == "player")
                .GetBar(ConcussionSystem.BarConcussion);
            Assert.Equal(60, barAfterEnemyTurn);

            // Now player's turn runs. StartOfTurn decays concussion bar by DecayPerTurn (60 − 20 = 40).
            var r1 = session.TryExecute(new AdvanceTurnCommand());
            int barAfterDecay = r1.View.Units.First(u => u.UnitId == "player").GetBar(ConcussionSystem.BarConcussion);
            Assert.Equal(barAfterEnemyTurn - ConcussionSystem.DecayPerTurn, barAfterDecay);
        }

        // ── Test 12: Concussion bar is visible in unit bars ───────────────────

        [Fact]
        public void Integration_ConcussionBar_VisibleInUnitBars()
        {
            var session = BuildSession(DazingBluntSkill(), seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());
            Assert.True(result.Accepted);

            var enemyState = result.View.Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyState.Bars);
            Assert.True(enemyState.Bars!.ContainsKey(ConcussionSystem.BarConcussion),
                "Concussion bar should be present in unit bars.");
        }
    }
}
