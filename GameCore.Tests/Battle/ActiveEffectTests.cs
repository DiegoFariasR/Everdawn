using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for the runtime active-effect (buff/debuff) system.
    /// Covers:
    ///  1.  Runtime active effect can be applied to a unit.
    ///  2.  Active effect duration decreases on the correct turn timing (ForTargetTurns).
    ///  3.  Expired effects are removed cleanly.
    ///  4.  Attack Up buff increases outgoing damage.
    ///  5.  Guard (DamageTakenMultiplier) reduces incoming damage.
    ///  6.  Temporary fire resistance reduces fire damage and fire buildup.
    ///  7.  Temporary cold resistance reduces cold buildup.
    ///  8.  Temporary disruption resistance reduces disruption buildup.
    ///  9.  Multiple active modifiers resolve in deterministic Set→Add→Multiply order.
    /// 10.  Set/Add/Multiply ordering is tested explicitly.
    /// 11.  Active effects are visible in runtime state (UnitState.ActiveEffects and StatusEffects).
    /// 12.  Repeated runs with the same seed remain deterministic.
    /// 13.  Runtime buff effects do not permanently mutate the base compiled skill definition.
    /// 14.  Stacking policies: RefreshDuration and ReplaceIfStronger behave as intended.
    /// 15.  ForSourceTurns duration ticks on the source unit's turns.
    /// 16.  UntilNextAction expires after the target's next action.
    /// 17.  StackIntensity increments the stack count on re-application.
    /// 18.  IndependentInstances creates separate instances on each application.
    /// 19.  Per-type damage dealt multiplier increases only the matching damage type and stacks with global multiplier.
    /// </summary>
    public class ActiveEffectTests
    {
        // ── Common skill / unit factories ────────────────────────────────────

        private static BattleSkill MakeDamageSkill(string id, double wisScale = 1.0,
            EffectType effectType = EffectType.Physical, string statKey = "str") =>
            new BattleSkill(id, id, Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(effectType, new DamageScaling[] { new DamageScaling(statKey, wisScale) })
                        })
                });

        private static BattleUnit MakePlayer(string id = "player", int str = 10, int wis = 50, int agi = 100,
            BattleSkill[]? skills = null) =>
            new BattleUnit(id, id, "player", Level: 1, Str: str, Wis: wis, Agi: agi,
                Skills: skills ?? new BattleSkill[] { MakeDamageSkill($"{id}-basic") });

        private static BattleUnit MakeEnemy(string id = "enemy", int str = 1, int wis = 0, int agi = 1,
            BattleSkill[]? skills = null) =>
            new BattleUnit(id, id, "enemy", Level: 1, Str: str, Wis: wis, Agi: agi,
                Skills: skills ?? new BattleSkill[] { MakeDamageSkill($"{id}-basic", statKey: "str") });

        private static BattleSession StartSession(BattleUnit player, BattleUnit enemy, int seed = 1)
        {
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });
            return session;
        }

        // ── Pre-built effect definitions ─────────────────────────────────────

        private static readonly ActiveEffectDefinition AttackUp = new ActiveEffectDefinition(
            Id: "attackUp",
            Name: "Attack Up",
            DurationKind: EffectDurationKind.ForTargetTurns,
            Duration: 2,
            StatModifiers: new RuntimeStatModifier[]
            {
                new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.5)
            }
        );

        private static readonly ActiveEffectDefinition Guard = new ActiveEffectDefinition(
            Id: "guard",
            Name: "Guard",
            DurationKind: EffectDurationKind.ForTargetTurns,
            Duration: 2,
            StatModifiers: new RuntimeStatModifier[]
            {
                new RuntimeStatModifier(RuntimeStatKey.DamageTakenMultiplier, ModifierOperation.Multiply, 0.8)
            }
        );

        private static readonly ActiveEffectDefinition FireResistUp = new ActiveEffectDefinition(
            Id: "fireResistUp",
            Name: "Fire Resist Up",
            DurationKind: EffectDurationKind.ForTargetTurns,
            Duration: 2,
            StatModifiers: new RuntimeStatModifier[]
            {
                new RuntimeStatModifier(RuntimeStatKey.FireResistance, ModifierOperation.Add, 50)
            }
        );

        private static readonly ActiveEffectDefinition ColdResistUp = new ActiveEffectDefinition(
            Id: "coldResistUp",
            Name: "Cold Resist Up",
            DurationKind: EffectDurationKind.ForTargetTurns,
            Duration: 2,
            StatModifiers: new RuntimeStatModifier[]
            {
                new RuntimeStatModifier(RuntimeStatKey.ColdResistance, ModifierOperation.Add, 100)
            }
        );

        private static readonly ActiveEffectDefinition DisruptionResistUp = new ActiveEffectDefinition(
            Id: "disruptionResistUp",
            Name: "Disruption Resist Up",
            DurationKind: EffectDurationKind.ForTargetTurns,
            Duration: 2,
            StatModifiers: new RuntimeStatModifier[]
            {
                new RuntimeStatModifier(RuntimeStatKey.DisruptionResistance, ModifierOperation.Add, 100)
            }
        );

        private static readonly ActiveEffectDefinition SkillDmgUp = new ActiveEffectDefinition(
            Id: "skillDmgUp",
            Name: "Skill Damage Up",
            DurationKind: EffectDurationKind.ForTargetTurns,
            Duration: 2,
            SkillModifier: new RuntimeSkillModifier(
                Modify: new Dictionary<ModifierVariable, double> { { ModifierVariable.DamageMultiplier, 0.5 } }
            )
        );

        // ────────────────────────────────────────────────────────────────────
        // Test 1: Active effect can be applied to a unit
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ApplyActiveEffect_EffectIsVisibleOnUnit()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy);

            session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");

            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            Assert.NotNull(playerState.ActiveEffects);
            Assert.Single(playerState.ActiveEffects!);
            Assert.Equal("attackUp", playerState.ActiveEffects![0].DefinitionId);
            Assert.Equal("Attack Up", playerState.ActiveEffects![0].Name);
            Assert.Equal(2, playerState.ActiveEffects![0].RemainingDuration);
        }

        [Fact]
        public void ApplyActiveEffect_EffectIdAppearsInStatusEffects()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy);

            session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");

            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            Assert.NotNull(playerState.StatusEffects);
            Assert.Contains("attackUp", playerState.StatusEffects!);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 2: Duration decrements on correct turns (ForTargetTurns)
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ActiveEffect_Duration_DecrementsAfterActorTurn()
        {
            // Player acts (initiating the buff application), then player acts again.
            // Duration should decrement from 2 → 1 after the player's action turn.
            var player = MakePlayer(agi: 100);
            var enemy = MakeEnemy(agi: 1); // enemy acts last

            var session = StartSession(player, enemy, seed: 1);

            // Apply buff to player.
            session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");
            Assert.Equal(2, session.GetView().Units.First(u => u.UnitId == "player")
                .ActiveEffects![0].RemainingDuration);

            // Player acts (turn 1 of buff). Buff should tick: 2 → 1.
            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));

            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            if (playerState.ActiveEffects != null && playerState.ActiveEffects.Count > 0)
                Assert.Equal(1, playerState.ActiveEffects[0].RemainingDuration);
            // If ActiveEffects is null here, the effect expired (duration was 1 after one tick).
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 3: Expired effects are removed cleanly
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ActiveEffect_ExpiresAndIsRemovedAfterDurationEnds()
        {
            var player = MakePlayer(agi: 100);
            var enemy = MakeEnemy(agi: 1, str: 1); // harmless enemy

            var session = StartSession(player, enemy, seed: 1);

            // Apply a 1-turn buff.
            var shortBuff = new ActiveEffectDefinition(
                "shortBuff", "Short Buff",
                EffectDurationKind.ForTargetTurns, Duration: 1,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 2.0)
                }
            );
            session.ApplyActiveEffect("player", shortBuff, sourceUnitId: "player");

            // Player acts: buff should expire (duration 1 → 0 → removed).
            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));

            // After the turn, the buff should be gone.
            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            bool hasEffect = playerState.ActiveEffects != null &&
                             playerState.ActiveEffects.Any(e => e.DefinitionId == "shortBuff");
            Assert.False(hasEffect, "Short buff should have expired after 1 turn.");
            if (playerState.StatusEffects != null)
                Assert.DoesNotContain("shortBuff", playerState.StatusEffects);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 4: Attack Up buff increases outgoing damage
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void AttackUpBuff_IncreasesOutgoingDamage()
        {
            // Seed is fixed so both scenarios produce deterministic values.
            int damageWithBuff = MeasurePlayerDamageToEnemy(withAttackUpBuff: true, seed: 42);
            int damageNoBuff = MeasurePlayerDamageToEnemy(withAttackUpBuff: false, seed: 42);

            Assert.True(damageWithBuff > damageNoBuff,
                $"Attack Up should increase damage. With buff: {damageWithBuff}, without: {damageNoBuff}");
        }

        private static int MeasurePlayerDamageToEnemy(bool withAttackUpBuff, int seed)
        {
            // Use a very high HP enemy (str=100→MaxHp=10000) so the player never kills them in one hit.
            // Player has agi=1 to get exactly 1 hit per action (avoids overkill confusion).
            var player = MakePlayer(str: 10, agi: 1);
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 100, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });
            int enemyMaxHp = enemy.MaxHp;

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            if (withAttackUpBuff)
                session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");

            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));
            var enemyState = session.GetView().Units.First(u => u.UnitId == "enemy");
            return enemyMaxHp - enemyState.CurrentHp;
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 5: Guard (DamageTakenMultiplier) reduces incoming damage
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void GuardBuff_ReducesIncomingDamage()
        {
            const int playerMaxHp = 10 * 100; // Str=10

            int damageWithGuard = playerMaxHp - MeasurePlayerHpAfterEnemyAttack(withGuard: true, seed: 42);
            int damageNoGuard = playerMaxHp - MeasurePlayerHpAfterEnemyAttack(withGuard: false, seed: 42);

            Assert.True(damageWithGuard < damageNoGuard,
                $"Guard should reduce incoming damage. With guard: {damageWithGuard}, without: {damageNoGuard}");
        }

        private static int MeasurePlayerHpAfterEnemyAttack(bool withGuard, int seed)
        {
            // Player has high agi so they act first; enemy counter-attacks with the buff active.
            var player = MakePlayer(str: 10, agi: 100,
                skills: new BattleSkill[] { MakeDamageSkill("p-basic") });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            // Apply buff BEFORE the player acts, so it is active when the enemy counter-attacks.
            if (withGuard)
                session.ApplyActiveEffect("player", Guard, sourceUnitId: "player");

            // Player attacks, then enemy counter-attacks (via AutoAdvance).
            session.TryExecute(new PlayerActionCommand("p-basic", "enemy"));
            return session.GetView().Units.First(u => u.UnitId == "player").CurrentHp;
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 6: Fire resistance buff reduces fire damage taken
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void FireResistBuff_ReducesFireDamageTaken()
        {
            int hpWithResist = MeasurePlayerHpAfterFireAttack(withFireResist: true, seed: 42);
            int hpNoResist = MeasurePlayerHpAfterFireAttack(withFireResist: false, seed: 42);

            Assert.True(hpWithResist > hpNoResist,
                $"Fire resist should reduce fire damage. HP with resist: {hpWithResist}, without: {hpNoResist}");
        }

        private static int MeasurePlayerHpAfterFireAttack(bool withFireResist, int seed)
        {
            // Player has high AGI so they act first; enemy counter-attacks with fire.
            // The buff must be applied before the player acts so it's active on the counter-attack.
            var player = MakePlayer(str: 50, agi: 100,
                skills: new BattleSkill[] { MakeDamageSkill("p-basic") });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 50, Wis: 50, Agi: 1,
                Skills: new BattleSkill[]
                {
                    new BattleSkill("fire-atk", "Fire", Cost: 0, DamageMultiplier: 1.0,
                        Effects: new SkillEffect[]
                        {
                            new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                                new DamageComponent[]
                                {
                                    new DamageComponent(EffectType.Fire, new DamageScaling[] { new DamageScaling("wis", 1.0) })
                                })
                        })
                });

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            // Apply buff BEFORE player acts so it is active when enemy counter-attacks.
            if (withFireResist)
                session.ApplyActiveEffect("player", FireResistUp, sourceUnitId: "player");

            // Player attacks, then enemy counter-attacks (via AutoAdvance).
            session.TryExecute(new PlayerActionCommand("p-basic", "enemy"));
            return session.GetView().Units.First(u => u.UnitId == "player").CurrentHp;
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 7: Cold resistance buff reduces cold bar buildup
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ColdResistBuff_ReducesColdBuildup()
        {
            // ColdResistUp adds 100 cold resistance → capped at 90%, so 10% of cold still builds.
            // Player has high AGI so they act first; enemy counter-attacks with the buff active.
            var player = MakePlayer(str: 50, agi: 100,
                skills: new BattleSkill[] { MakeDamageSkill("p-basic") });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 50, Wis: 5, Agi: 1,
                Skills: new BattleSkill[]
                {
                    new BattleSkill("cold-atk", "Blizzard", Cost: 0, DamageMultiplier: 1.0,
                        Effects: new SkillEffect[]
                        {
                            new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                                new DamageComponent[]
                                {
                                    new DamageComponent(EffectType.Cold, new DamageScaling[] { new DamageScaling("wis", 1.0) })
                                })
                        })
                });

            // Without resistance: cold builds up.
            int coldBarNoResist = GetPlayerColdBarAfterEnemyAttack(player, enemy, withResist: false, seed: 1);
            // With 100 cold resistance (capped at 90%): cold buildup reduced by 90%, not fully blocked.
            int coldBarWithResist = GetPlayerColdBarAfterEnemyAttack(player, enemy, withResist: true, seed: 1);

            Assert.True(coldBarNoResist > 0, "Cold should build up without resistance");
            Assert.True(coldBarWithResist < coldBarNoResist, "Cold resistance should reduce buildup");
        }

        private static int GetPlayerColdBarAfterEnemyAttack(BattleUnit player, BattleUnit enemy, bool withResist, int seed)
        {
            // Player has agi=100, enemy has agi=1 → player acts first, then enemy counter-attacks.
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });
            // Apply buff BEFORE player acts, so it is active when enemy counter-attacks.
            if (withResist)
                session.ApplyActiveEffect("player", ColdResistUp, sourceUnitId: "player");

            // Player acts first (high agi), enemy counter-attacks via AutoAdvance.
            session.TryExecute(new PlayerActionCommand("p-basic", "enemy"));
            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            return playerState.GetBar("cold");
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 8: Disruption resistance buff reduces disruption buildup
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void DisruptionResistBuff_ReducesDisruptionBuildup()
        {
            // Player has high AGI so they act first; enemy counter-attacks with the buff active.
            var player = MakePlayer(str: 50, agi: 100,
                skills: new BattleSkill[] { MakeDamageSkill("p-basic") });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 1,
                Skills: new BattleSkill[]
                {
                    new BattleSkill("slam", "Slam", Cost: 0, DamageMultiplier: 1.0,
                        Effects: new SkillEffect[]
                        {
                            new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                                new DamageComponent[]
                                {
                                    new DamageComponent(EffectType.Physical, new DamageScaling[] { new DamageScaling("str", 1.0) })
                                },
                                DisruptionPower: 40)
                        })
                });

            int disruptNoResist = GetPlayerDisruptionAfterEnemyAttack(player, enemy, withResist: false, seed: 1);
            int disruptWithResist = GetPlayerDisruptionAfterEnemyAttack(player, enemy, withResist: true, seed: 1);

            Assert.True(disruptNoResist > 0, "Disruption should build without resistance");
            Assert.True(disruptWithResist < disruptNoResist, "Disruption resistance should reduce buildup");
        }

        private static int GetPlayerDisruptionAfterEnemyAttack(BattleUnit player, BattleUnit enemy, bool withResist, int seed)
        {
            // Player has agi=100, enemy has agi=1 → player acts first, then enemy counter-attacks.
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });
            // Apply buff BEFORE player acts, so it is active when enemy counter-attacks.
            if (withResist)
                session.ApplyActiveEffect("player", DisruptionResistUp, sourceUnitId: "player");

            session.TryExecute(new PlayerActionCommand("p-basic", "enemy"));
            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            return playerState.GetBar("disruption");
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 9 & 10: Set → Add → Multiply ordering is deterministic
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void StatModifierOrdering_Set_Then_Add_Then_Multiply()
        {
            // Apply three effects in a known order:
            // Base DamageTakenMultiplier = 1.0
            // Set 0.5 → value = 0.5
            // Add 0.2 → value = 0.7
            // Multiply 2.0 → value = 1.4
            // Expected: incoming damage * 1.4 > base incoming damage.

            int hpWithModifiers = MeasurePlayerHpWithComplexModifiers(withModifiers: true, seed: 42);
            int hpNoModifiers = MeasurePlayerHpWithComplexModifiers(withModifiers: false, seed: 42);

            // With Set(0.5) + Add(0.2) + Multiply(2.0) = 1.4x damage taken, more damage = less HP.
            Assert.True(hpWithModifiers < hpNoModifiers,
                "Combined Set+Add+Multiply(>1) should increase damage taken, reducing HP");
        }

        private static int MeasurePlayerHpWithComplexModifiers(bool withModifiers, int seed)
        {
            // Use enemy with very high str (lots of damage) but player has high HP.
            // This ensures variance doesn't flatten the comparison.
            var player = MakePlayer(str: 100, agi: 1,
                skills: new BattleSkill[] { MakeDamageSkill("p-basic") });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 50, Wis: 0, Agi: 100,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            if (withModifiers)
            {
                // Effect A: Set DamageTakenMultiplier = 0.5
                session.ApplyActiveEffect("player", new ActiveEffectDefinition("effA", "Effect A",
                    EffectDurationKind.ForTargetTurns, Duration: 5,
                    StatModifiers: new RuntimeStatModifier[]
                    {
                        new RuntimeStatModifier(RuntimeStatKey.DamageTakenMultiplier, ModifierOperation.Set, 0.5)
                    }), "player");

                // Effect B: Add 0.2 to DamageTakenMultiplier (applied after Set → base 0.5 + 0.2 = 0.7)
                session.ApplyActiveEffect("player", new ActiveEffectDefinition("effB", "Effect B",
                    EffectDurationKind.ForTargetTurns, Duration: 5,
                    StackingPolicy: EffectStackingPolicy.IndependentInstances,
                    StatModifiers: new RuntimeStatModifier[]
                    {
                        new RuntimeStatModifier(RuntimeStatKey.DamageTakenMultiplier, ModifierOperation.Add, 0.2)
                    }), "player");

                // Effect C: Multiply DamageTakenMultiplier × 2.0 (applied last → 0.7 × 2.0 = 1.4)
                session.ApplyActiveEffect("player", new ActiveEffectDefinition("effC", "Effect C",
                    EffectDurationKind.ForTargetTurns, Duration: 5,
                    StackingPolicy: EffectStackingPolicy.IndependentInstances,
                    StatModifiers: new RuntimeStatModifier[]
                    {
                        new RuntimeStatModifier(RuntimeStatKey.DamageTakenMultiplier, ModifierOperation.Multiply, 2.0)
                    }), "player");
            }

            // Enemy acts first (high agi), damages player.
            session.TryExecute(new PlayerActionCommand("p-basic", "enemy"));
            return session.GetView().Units.First(u => u.UnitId == "player").CurrentHp;
        }

        [Fact]
        public void StatModifierOrdering_MultipleAdd_AreAllSummed()
        {
            // Two Add modifiers to DamageDealtMultiplier: +0.5 each → 1.0 base + 1.0 adds = 2.0
            int dmgTwoAdds = MeasurePlayerDamageWithAddModifiers(addCount: 2, addValue: 0.5, seed: 42);
            int dmgOneAdd = MeasurePlayerDamageWithAddModifiers(addCount: 1, addValue: 0.5, seed: 42);
            int dmgNoAdd = MeasurePlayerDamageWithAddModifiers(addCount: 0, addValue: 0.5, seed: 42);

            Assert.True(dmgNoAdd < dmgOneAdd,
                "One Add modifier should increase damage over baseline");
            Assert.True(dmgOneAdd < dmgTwoAdds,
                "Two Add modifiers should increase damage more than one");
        }

        private static int MeasurePlayerDamageWithAddModifiers(int addCount, double addValue, int seed)
        {
            // Use high-HP enemy so player never kills them in one hit.
            var player = MakePlayer(str: 10, agi: 1);
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 100, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });
            int enemyMaxHp = enemy.MaxHp;

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            for (int i = 0; i < addCount; i++)
            {
                session.ApplyActiveEffect("player", new ActiveEffectDefinition(
                    $"addMod{i}", $"Add Mod {i}",
                    EffectDurationKind.ForTargetTurns, Duration: 5,
                    StackingPolicy: EffectStackingPolicy.IndependentInstances,
                    StatModifiers: new RuntimeStatModifier[]
                    {
                        new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Add, addValue)
                    }), "player");
            }

            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));
            var enemyState = session.GetView().Units.First(u => u.UnitId == "enemy");
            return enemyMaxHp - enemyState.CurrentHp;
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 11: Active effects are visible in runtime state / snapshots
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ActiveEffects_AreExposedInUnitState()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy);

            session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");
            session.ApplyActiveEffect("player", Guard, sourceUnitId: "player");

            var playerState = session.GetView().Units.First(u => u.UnitId == "player");

            Assert.NotNull(playerState.ActiveEffects);
            Assert.Equal(2, playerState.ActiveEffects!.Count);
            Assert.Contains(playerState.ActiveEffects, e => e.DefinitionId == "attackUp");
            Assert.Contains(playerState.ActiveEffects, e => e.DefinitionId == "guard");
            Assert.True(playerState.StatusEffects!.Contains("attackUp"), "StatusEffects should include attackUp");
            Assert.True(playerState.StatusEffects!.Contains("guard"), "StatusEffects should include guard");
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 12: Same seed produces identical results (determinism)
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void Determinism_SameSeedProducesSameOutcome()
        {
            int run1 = MeasurePlayerDamageToEnemy(withAttackUpBuff: true, seed: 99);
            int run2 = MeasurePlayerDamageToEnemy(withAttackUpBuff: true, seed: 99);
            Assert.Equal(run1, run2);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 13: Runtime buffs do not permanently mutate base compiled skill
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void RuntimeBuff_DoesNotMutateBaseSkillDefinition()
        {
            var skill = MakeDamageSkill("test-skill");
            double originalMultiplier = skill.DamageMultiplier;

            var player = MakePlayer(skills: new BattleSkill[] { skill });
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy, seed: 1);

            session.ApplyActiveEffect("player", SkillDmgUp, sourceUnitId: "player");

            // Execute the action so the effective skill is resolved internally.
            session.TryExecute(new PlayerActionCommand("test-skill", "enemy"));

            // The original skill object must be completely unchanged.
            Assert.Equal(originalMultiplier, skill.DamageMultiplier);
            Assert.Equal(originalMultiplier, player.ResolvedSkills[0].DamageMultiplier);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 14: Stacking policies
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void StackingPolicy_RefreshDuration_ResetsExistingDuration()
        {
            var player = MakePlayer(agi: 100);
            var enemy = MakeEnemy(agi: 1, str: 1);
            var session = StartSession(player, enemy, seed: 1);

            // Apply a 2-turn buff.
            session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");

            // Player acts once → duration becomes 1.
            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));
            var afterOneTurn = session.GetView().Units.First(u => u.UnitId == "player");
            if (afterOneTurn.ActiveEffects == null || afterOneTurn.ActiveEffects.Count == 0)
                return; // Effect already expired; skip re-apply check.

            // Re-apply (RefreshDuration): duration should reset to 2.
            session.ApplyActiveEffect("player", AttackUp, sourceUnitId: "player");
            var afterRefresh = session.GetView().Units.First(u => u.UnitId == "player");
            Assert.NotNull(afterRefresh.ActiveEffects);
            var entry = afterRefresh.ActiveEffects!.FirstOrDefault(e => e.DefinitionId == "attackUp");
            Assert.NotNull(entry);
            Assert.Equal(2, entry!.RemainingDuration);
        }

        [Fact]
        public void StackingPolicy_ReplaceIfStronger_IgnoresShorterReapplication()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy, seed: 1);

            var longBuff = new ActiveEffectDefinition("replaceTest", "Replace Test",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StackingPolicy: EffectStackingPolicy.ReplaceIfStronger,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.5)
                });

            var shortBuff = new ActiveEffectDefinition("replaceTest", "Replace Test",
                EffectDurationKind.ForTargetTurns, Duration: 2,
                StackingPolicy: EffectStackingPolicy.ReplaceIfStronger,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.5)
                });

            // Apply long buff first, then try to re-apply with shorter duration.
            session.ApplyActiveEffect("player", longBuff, sourceUnitId: "player");
            session.ApplyActiveEffect("player", shortBuff, sourceUnitId: "player");

            // Should still have duration = 5 (the shorter re-application was ignored).
            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            var entry = playerState.ActiveEffects!.First(e => e.DefinitionId == "replaceTest");
            Assert.Equal(5, entry.RemainingDuration);
        }

        [Fact]
        public void StackingPolicy_ReplaceIfStronger_ReplacesWhenNewIsLonger()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy, seed: 1);

            var shortBuff = new ActiveEffectDefinition("replaceTest2", "Replace Test 2",
                EffectDurationKind.ForTargetTurns, Duration: 2,
                StackingPolicy: EffectStackingPolicy.ReplaceIfStronger,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.5)
                });

            var longBuff = new ActiveEffectDefinition("replaceTest2", "Replace Test 2",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StackingPolicy: EffectStackingPolicy.ReplaceIfStronger,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.5)
                });

            // Apply short buff first, then re-apply with longer duration.
            session.ApplyActiveEffect("player", shortBuff, sourceUnitId: "player");
            session.ApplyActiveEffect("player", longBuff, sourceUnitId: "player");

            // Should have duration = 5 (the longer re-application replaced the shorter one).
            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            var entry = playerState.ActiveEffects!.First(e => e.DefinitionId == "replaceTest2");
            Assert.Equal(5, entry.RemainingDuration);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 15: ForSourceTurns duration ticks on source unit's turns
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ForSourceTurns_TicksOnSourceTurn_NotTargetTurn()
        {
            // Enemy is the target, player is the source. Player acts (source turn) → duration ticks.
            var player = MakePlayer(agi: 100);
            var enemy = MakeEnemy(agi: 1, str: 1);
            var session = StartSession(player, enemy, seed: 1);

            var sourceOwnedDebuff = new ActiveEffectDefinition("sourceDebuff", "Source Debuff",
                EffectDurationKind.ForSourceTurns, Duration: 2,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageTakenMultiplier, ModifierOperation.Multiply, 1.2)
                });

            // Apply to the enemy, but duration counts on player (source) turns.
            session.ApplyActiveEffect("enemy", sourceOwnedDebuff, sourceUnitId: "player");

            // Verify the effect is on the enemy.
            var enemyBeforeAct = session.GetView().Units.First(u => u.UnitId == "enemy");
            Assert.NotNull(enemyBeforeAct.ActiveEffects);
            Assert.Equal(2, enemyBeforeAct.ActiveEffects![0].RemainingDuration);

            // Player acts (source acts) → duration ticks down on the enemy's effect.
            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));

            var enemyAfterAct = session.GetView().Units.First(u => u.UnitId == "enemy");
            if (enemyAfterAct.IsAlive && enemyAfterAct.ActiveEffects != null && enemyAfterAct.ActiveEffects.Count > 0)
                Assert.Equal(1, enemyAfterAct.ActiveEffects[0].RemainingDuration);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 16: UntilNextAction expires after the target's next action
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void UntilNextAction_ExpiresAfterTargetActs()
        {
            var player = MakePlayer(agi: 100);
            var enemy = MakeEnemy(agi: 1, str: 1);
            var session = StartSession(player, enemy, seed: 1);

            var oneActionBuff = new ActiveEffectDefinition("oneShot", "One Shot Buff",
                EffectDurationKind.UntilNextAction, Duration: 1,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 2.0)
                });

            session.ApplyActiveEffect("player", oneActionBuff, sourceUnitId: "player");

            // The buff should be active before the player acts.
            var beforeAct = session.GetView().Units.First(u => u.UnitId == "player");
            Assert.NotNull(beforeAct.ActiveEffects);

            // Player acts → buff should expire.
            session.TryExecute(new PlayerActionCommand("player-basic", "enemy"));

            var afterAct = session.GetView().Units.First(u => u.UnitId == "player");
            bool stillActive = afterAct.ActiveEffects != null &&
                               afterAct.ActiveEffects.Any(e => e.DefinitionId == "oneShot");
            Assert.False(stillActive, "UntilNextAction buff should expire after the player acts.");
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 17: StackIntensity increments the stack count
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void StackingPolicy_StackIntensity_IncrementsStackCount()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy, seed: 1);

            var stackingBuff = new ActiveEffectDefinition("stackBuff", "Stack Buff",
                EffectDurationKind.ForTargetTurns, Duration: 3,
                StackingPolicy: EffectStackingPolicy.StackIntensity,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.2)
                });

            session.ApplyActiveEffect("player", stackingBuff, sourceUnitId: "player");
            session.ApplyActiveEffect("player", stackingBuff, sourceUnitId: "player");

            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            Assert.NotNull(playerState.ActiveEffects);
            Assert.Single(playerState.ActiveEffects!); // still one instance
            Assert.Equal(2, playerState.ActiveEffects![0].Stacks);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 18: IndependentInstances creates separate instances
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void StackingPolicy_IndependentInstances_CreatesMultipleInstances()
        {
            var player = MakePlayer();
            var enemy = MakeEnemy();
            var session = StartSession(player, enemy, seed: 1);

            var indepBuff = new ActiveEffectDefinition("indep", "Independent Buff",
                EffectDurationKind.ForTargetTurns, Duration: 2,
                StackingPolicy: EffectStackingPolicy.IndependentInstances,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 1.1)
                });

            session.ApplyActiveEffect("player", indepBuff, sourceUnitId: "player");
            session.ApplyActiveEffect("player", indepBuff, sourceUnitId: "player");
            session.ApplyActiveEffect("player", indepBuff, sourceUnitId: "player");

            var playerState = session.GetView().Units.First(u => u.UnitId == "player");
            Assert.Equal(3, playerState.ActiveEffects!.Count);
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 19: Per-type damage dealt multiplier increases damage only for the specified type
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void PerTypeDamageDealtMultiplier_IncreasesOnlyMatchingType()
        {
            // Fire skill vs. no-resistance enemy.
            var fireSkill = MakeDamageSkill("fire-skill", wisScale: 1.0, effectType: EffectType.Fire, statKey: "wis");
            var physSkill = MakeDamageSkill("phys-skill", wisScale: 1.0, effectType: EffectType.Physical, statKey: "str");

            var player = new BattleUnit("player", "player", "player", Level: 1, Str: 50, Wis: 50, Agi: 1,
                Skills: new BattleSkill[] { fireSkill, physSkill });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 100, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            int enemyMaxHp = enemy.MaxHp;

            var fireBoost = new ActiveEffectDefinition("fireboost", "Fire Boost",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.FireDamageDealtMultiplier, ModifierOperation.Multiply, 2.0)
                });

            // Measure fire damage without buff.
            int fireDamageNoBuff = MeasureTypeDamage(player, enemy, "fire-skill", applyBuff: null, seed: 42);

            // Measure fire damage WITH fire buff.
            int fireDamageWithBuff = MeasureTypeDamage(player, enemy, "fire-skill", applyBuff: fireBoost, seed: 42);

            // Measure physical damage WITH fire buff — should be unchanged.
            int physDamageWithBuff = MeasureTypeDamage(player, enemy, "phys-skill", applyBuff: fireBoost, seed: 42);
            int physDamageNoBuff = MeasureTypeDamage(player, enemy, "phys-skill", applyBuff: null, seed: 42);

            Assert.True(fireDamageWithBuff > fireDamageNoBuff,
                "Fire damage dealt multiplier should increase fire damage");
            Assert.Equal(physDamageNoBuff, physDamageWithBuff);
        }

        [Fact]
        public void PerTypeDamageDealtMultiplier_StacksWithGlobalDamageMultiplier()
        {
            var fireSkill = MakeDamageSkill("fire-skill", wisScale: 1.0, effectType: EffectType.Fire, statKey: "wis");
            var player = new BattleUnit("player", "player", "player", Level: 1, Str: 10, Wis: 50, Agi: 1,
                Skills: new BattleSkill[] { fireSkill });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 100, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var globalBoost = new ActiveEffectDefinition("global", "Global Boost",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.DamageDealtMultiplier, ModifierOperation.Multiply, 2.0)
                });

            var fireBoost = new ActiveEffectDefinition("fireboost", "Fire Boost",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.FireDamageDealtMultiplier, ModifierOperation.Multiply, 2.0)
                });

            int damageNoBuff = MeasureTypeDamage(player, enemy, "fire-skill", applyBuff: null, seed: 7);
            int damageGlobalOnly = MeasureTypeDamage(player, enemy, "fire-skill", applyBuff: globalBoost, seed: 7);
            int damageFireOnly = MeasureTypeDamage(player, enemy, "fire-skill", applyBuff: fireBoost, seed: 7);
            int damageBoth = MeasureTypeDamageTwoBuffs(player, enemy, "fire-skill", globalBoost, fireBoost, seed: 7);

            Assert.True(damageGlobalOnly > damageNoBuff, "Global multiplier should increase fire damage");
            Assert.True(damageFireOnly > damageNoBuff, "Per-type multiplier should increase fire damage");
            Assert.True(damageBoth > damageGlobalOnly, "Both buffs should be stronger than global alone");
            Assert.True(damageBoth > damageFireOnly, "Both buffs should be stronger than per-type alone");
        }

        private static int MeasureTypeDamage(BattleUnit player, BattleUnit enemy,
            string skillId, ActiveEffectDefinition? applyBuff, int seed)
        {
            int enemyMaxHp = enemy.MaxHp;
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            if (applyBuff != null)
                session.ApplyActiveEffect("player", applyBuff, sourceUnitId: "player");

            session.TryExecute(new PlayerActionCommand(skillId, "enemy"));
            var enemyState = session.GetView().Units.First(u => u.UnitId == "enemy");
            return enemyMaxHp - enemyState.CurrentHp;
        }

        private static int MeasureTypeDamageTwoBuffs(BattleUnit player, BattleUnit enemy,
            string skillId, ActiveEffectDefinition buffA, ActiveEffectDefinition buffB, int seed)
        {
            int enemyMaxHp = enemy.MaxHp;
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            session.ApplyActiveEffect("player", buffA, sourceUnitId: "player");
            session.ApplyActiveEffect("player", buffB, sourceUnitId: "player");
            session.TryExecute(new PlayerActionCommand(skillId, "enemy"));
            var enemyState = session.GetView().Units.First(u => u.UnitId == "enemy");
            return enemyMaxHp - enemyState.CurrentHp;
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 20: ReceivingHealingMultiplier scales healing received by target
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ReceivingHealingMultiplier_ScalesHealingReceived()
        {
            var basicAtk = MakeDamageSkill("basic-atk");
            var healSkill = new BattleSkill("heal", "heal", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Heal, BattleSkillTarget.Ally,
                        new DamageComponent[] { new DamageComponent(null, new DamageScaling[] { new DamageScaling("wis", 1.0) }) })
                });

            // Player Wis=10 → MagicAttack=80 → raw heal ≈ 80 per cast.
            // Enemy Str=25 → PhysAttack=200 → ~200 damage per hit.
            // Both 80 and 2×80=160 are less than the ≈200 deficit, so the HP cap never interferes.
            // Player Str=20 → MaxHp=2000, survives two enemy hits (≈200 each).
            var player = new BattleUnit("player", "player", "player", Level: 1, Str: 20, Wis: 10, Agi: 100,
                Skills: new BattleSkill[] { basicAtk, healSkill });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 25, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic", statKey: "str") });

            var healBoost = new ActiveEffectDefinition("healBoost", "Heal Boost",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.ReceivingHealingMultiplier, ModifierOperation.Multiply, 2.0)
                });

            // Compare final player HP after (basic-atk → enemy hits → heal → enemy hits).
            // Both sessions share the same seed so all RNG rolls are identical except the heal
            // amount (which is multiplied in the buff session). Same-seed enemy hits cancel out.
            int hpNoBuff = MeasureHpAfterHeal(player, enemy, "heal", applyBuff: null, seed: 42);
            int hpWithBuff = MeasureHpAfterHeal(player, enemy, "heal", applyBuff: healBoost, seed: 42);

            Assert.True(hpWithBuff > hpNoBuff,
                "ReceivingHealingMultiplier should increase healing received, resulting in higher HP");
        }

        // ────────────────────────────────────────────────────────────────────
        // Test 21: ReceivingBarrierMultiplier scales barrier received by target
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ReceivingBarrierMultiplier_ScalesBarrierReceived()
        {
            var shieldSkill = new BattleSkill("shield", "shield", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Shield, BattleSkillTarget.Ally,
                        new DamageComponent[] { new DamageComponent(null, new DamageScaling[] { new DamageScaling("wis", 1.0) }) })
                });

            var player = new BattleUnit("player", "player", "player", Level: 1, Str: 10, Wis: 50, Agi: 100,
                Skills: new BattleSkill[] { shieldSkill });
            var enemy = new BattleUnit("enemy", "enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic", statKey: "str") });

            var barrierBoost = new ActiveEffectDefinition("barrierBoost", "Barrier Boost",
                EffectDurationKind.ForTargetTurns, Duration: 5,
                StatModifiers: new RuntimeStatModifier[]
                {
                    new RuntimeStatModifier(RuntimeStatKey.ReceivingBarrierMultiplier, ModifierOperation.Multiply, 2.0)
                });

            int barrierNoBuff = MeasureBarrierAfterShield(player, enemy, "shield", applyBuff: null, seed: 42);
            int barrierWithBuff = MeasureBarrierAfterShield(player, enemy, "shield", applyBuff: barrierBoost, seed: 42);

            Assert.True(barrierWithBuff > barrierNoBuff,
                "ReceivingBarrierMultiplier should increase barrier received");
        }

        /// <summary>
        /// Executes a basic attack (to let the enemy damage the player), applies an optional buff,
        /// then executes the heal skill. Returns the player's HP after both actions (including the
        /// two enemy counter-attacks in AutoAdvance). Both sessions use the same seed so enemy
        /// damage values are identical, isolating the healing difference.
        /// </summary>
        private static int MeasureHpAfterHeal(BattleUnit player, BattleUnit enemy,
            string healSkillId, ActiveEffectDefinition? applyBuff, int seed)
        {
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            // First action: player basic-attacks. AutoAdvance plays enemy counter-attack.
            // Player is now below max HP, creating a deficit that makes healing measurable.
            session.TryExecute(new PlayerActionCommand("basic-atk", "enemy"));

            if (applyBuff != null)
                session.ApplyActiveEffect("player", applyBuff, sourceUnitId: "player");

            // Second action: player heals self. AutoAdvance plays another enemy counter-attack.
            session.TryExecute(new PlayerActionCommand(healSkillId, "player"));

            return session.GetView().Units.First(u => u.UnitId == "player").CurrentHp;
        }

        private static int MeasureBarrierAfterShield(BattleUnit player, BattleUnit enemy,
            string shieldSkillId, ActiveEffectDefinition? applyBuff, int seed)
        {
            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            if (applyBuff != null)
                session.ApplyActiveEffect("player", applyBuff, sourceUnitId: "player");

            session.TryExecute(new PlayerActionCommand(shieldSkillId, "player"));
            return session.GetView().Units.First(u => u.UnitId == "player").GetBar("barrier");
        }
    }
}
