using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class ReactionTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static BattleSkill MeleeSkill(string id = "melee-hit") =>
            new BattleSkill(id, "Melee Hit", Cost: 0, DamageMultiplier: 1.0,
                Range: SkillRange.Melee,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 1.0) })
                        }),
                });

        private static BattleSkill RangedSkill(string id = "ranged-hit") =>
            new BattleSkill(id, "Ranged Hit", Cost: 0, DamageMultiplier: 1.0,
                Range: SkillRange.Ranged,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 1.0) })
                        }),
                });

        private static BattleSkill CounterStrikeSkill(int cooldown = 2) =>
            new BattleSkill("counter-strike", "Counter Strike", Cost: 0, DamageMultiplier: 1.0,
                Range: SkillRange.Melee,
                Category: SkillCategory.Reaction,
                Trigger: ReactionTrigger.OnHitBy,
                TriggerConditions: new TriggerCondition[] { new TriggerCondition(Range: SkillRange.Melee) },
                Cooldown: cooldown,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.5) })
                        }),
                });

        // Player hits first (attackerAgi=200). Enemy has configurable reaction skill; high STR for survivability.
        private static BattleSession BuildSession(
            BattleSkill attackerSkill,
            BattleSkill? enemyReactionSkill = null,
            int attackerStr = 5,
            int attackerAgi = 200,
            int enemyStr = 500,
            int initialReactionCd = 0,
            int seed = 42)
        {
            var reaction = enemyReactionSkill ?? CounterStrikeSkill();
            var reactorSkills = new BattleSkill[] { new BattleSkill("enemy-attack", "Attack", Cost: 0,
                DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.1) })
                        })
                },
                Modifiers: new string[] { "basic" },
                ModifierTags: new string[] { "basic" }) };

            var player = new BattleUnit("player", "Player", "player",
                Level: 1, Str: attackerStr, Wis: 0, Agi: attackerAgi,
                Skills: new BattleSkill[] { attackerSkill });

            var enemy = new BattleUnit("enemy", "Enemy", "enemy",
                Level: 1, Str: enemyStr, Wis: 0, Agi: 1,
                Skills: reactorSkills,
                ReactionSkill: reaction);

            var session = new BattleSession(seed);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            // Pre-apply cooldown by overriding initial CD via a fresh session with the enemy having
            // a reaction skill that starts on CD.
            if (initialReactionCd > 0)
            {
                var reactionOnCd = CounterStrikeSkill(cooldown: 2) with { InitialCooldown = initialReactionCd };
                var enemyOnCd = new BattleUnit("enemy", "Enemy", "enemy",
                    Level: 1, Str: enemyStr, Wis: 0, Agi: 1,
                    Skills: reactorSkills,
                    ReactionSkill: reactionOnCd);
                session = new BattleSession(seed);
                session.Start(new BattleSetup
                {
                    PlayerUnits = new List<BattleUnit> { player },
                    EnemyUnits = new List<BattleUnit> { enemyOnCd },
                });
            }

            return session;
        }

        // ── Test 1: Counter-strike fires after melee hit ──────────────────────

        [Fact]
        public void CounterStrike_FiresAfterMeleeHit()
        {
            var session = BuildSession(MeleeSkill(), attackerStr: 5, enemyStr: 500, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            // A "reaction" event should appear in the log.
            Assert.Contains(result.View.FullLog, e => e.Type == "reaction");
        }

        // ── Test 2: Counter-strike does NOT fire after a ranged hit ───────────

        [Fact]
        public void CounterStrike_DoesNotFireAfterRangedHit()
        {
            var session = BuildSession(RangedSkill(), attackerStr: 5, enemyStr: 500, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            var reactionEvents = result.View.FullLog.Where(e => e.Type == "reaction").ToList();
            Assert.Empty(reactionEvents);
        }

        // ── Test 3: Counter-strike deals damage back to the attacker ─────────

        [Fact]
        public void CounterStrike_DealsDamageToAttacker()
        {
            var initialPlayerHp = new BattleUnit("p", "P", "player", 1, 5, 0, 200).MaxHp;
            var session = BuildSession(MeleeSkill(), attackerStr: 5, enemyStr: 500, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            int playerHp = result.View.Units.First(u => u.UnitId == "player").CurrentHp;
            Assert.True(playerHp < initialPlayerHp,
                "Counter-strike should deal damage to the original attacker.");
        }

        // ── Test 4: Counter-strike does NOT fire when on cooldown ─────────────

        [Fact]
        public void CounterStrike_DoesNotFireWhenOnCooldown()
        {
            // Start with the reaction on cooldown.
            var session = BuildSession(MeleeSkill(), attackerStr: 5, enemyStr: 500,
                initialReactionCd: 2, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            var reactionEvents = result.View.FullLog.Where(e => e.Type == "reaction").ToList();
            Assert.Empty(reactionEvents);
        }

        // ── Test 5: Counter-strike does NOT fire if the target dies ───────────

        [Fact]
        public void CounterStrike_DoesNotFireIfTargetKilledByAttack()
        {
            // Attacker has massive STR so the enemy is killed on the first hit.
            // Enemy has minimal HP (str=1 → MaxHp=100). Attacker str=1000 → damage >> 100.
            var session = BuildSession(MeleeSkill(), attackerStr: 1000, enemyStr: 1, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            var reactionEvents = result.View.FullLog.Where(e => e.Type == "reaction").ToList();
            Assert.Empty(reactionEvents);
        }

        // ── Test 6: Counter-strike can kill the original attacker ─────────────

        [Fact]
        public void CounterStrike_CanKillAttacker()
        {
            // Attacker has minimal survivability (str=1 → 100 HP).
            // Enemy has massive STR so its counter-strike (STR × 0.5) kills the attacker.
            var session = BuildSession(MeleeSkill(), attackerStr: 1, enemyStr: 10000, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            int playerHp = result.View.Units.First(u => u.UnitId == "player").CurrentHp;
            Assert.Equal(0, playerHp);
            Assert.True(result.View.IsOver, "Battle should be over after the attacker is killed.");
        }

        // ── Test 7: Counter-strike goes on cooldown after firing ──────────────

        [Fact]
        public void CounterStrike_GoesOnCooldownAfterFiring()
        {
            var session = BuildSession(MeleeSkill(), attackerStr: 5, enemyStr: 500, seed: 42);

            // Turn 1: player hits → counter fires, goes on CD.
            var r1 = session.TryExecute(new AdvanceTurnCommand());
            Assert.Contains(r1.View.FullLog, e => e.Type == "reaction");

            // Turn 2: player hits again → counter is on CD, should NOT fire.
            var r2 = session.TryExecute(new AdvanceTurnCommand());
            var newReactionEvents = r2.View.FullLog
                .Where(e => e.Type == "reaction")
                .Skip(r1.View.FullLog.Count(e => e.Type == "reaction"))
                .ToList();
            Assert.Empty(newReactionEvents);
        }

        // ── Test 9: OnHitBy with no conditions fires on any damaging hit ──────

        [Fact]
        public void OnHitBy_NoConditions_TriggersOnAnyDamagingHit()
        {
            // Reaction with OnHitBy and no TriggerConditions — fires on both melee and ranged.
            var unconditionalReaction = new BattleSkill("any-counter", "Any Counter",
                Cost: 0, DamageMultiplier: 1.0,
                Range: SkillRange.Melee,
                Category: SkillCategory.Reaction,
                Trigger: ReactionTrigger.OnHitBy,
                TriggerConditions: null,
                Cooldown: 0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.5) })
                        }),
                });

            var session = BuildSession(RangedSkill(), enemyReactionSkill: unconditionalReaction,
                attackerStr: 5, enemyStr: 500, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            // Should fire even on a ranged hit because there are no range conditions.
            Assert.Contains(result.View.FullLog, e => e.Type == "reaction");
        }

        // ── Test 10: OnHitBy with damageType condition ────────────────────────

        [Fact]
        public void OnHitBy_DamageTypeCondition_DoesNotFireOnNonMatchingType()
        {
            // Reaction that only fires on Fire damage.
            var fireCounter = new BattleSkill("fire-counter", "Fire Counter",
                Cost: 0, DamageMultiplier: 1.0,
                Range: SkillRange.Melee,
                Category: SkillCategory.Reaction,
                Trigger: ReactionTrigger.OnHitBy,
                TriggerConditions: new TriggerCondition[]
                {
                    new TriggerCondition(DamageType: EffectType.Fire),
                },
                Cooldown: 0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.5) })
                        }),
                });

            // Attacker uses a Physical melee skill — should NOT trigger the Fire condition.
            var session = BuildSession(MeleeSkill(), enemyReactionSkill: fireCounter,
                attackerStr: 5, enemyStr: 500, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            Assert.Empty(result.View.FullLog.Where(e => e.Type == "reaction").ToList());
        }

        [Fact]
        public void OnHitBy_DamageTypeCondition_FiresOnMatchingType()
        {
            // Reaction that only fires on Physical damage.
            var physCounter = new BattleSkill("phys-counter", "Phys Counter",
                Cost: 0, DamageMultiplier: 1.0,
                Range: SkillRange.Melee,
                Category: SkillCategory.Reaction,
                Trigger: ReactionTrigger.OnHitBy,
                TriggerConditions: new TriggerCondition[]
                {
                    new TriggerCondition(DamageType: EffectType.Physical),
                },
                Cooldown: 0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.5) })
                        }),
                });

            // Attacker uses a Physical melee skill — should trigger.
            var session = BuildSession(MeleeSkill(), enemyReactionSkill: physCounter,
                attackerStr: 5, enemyStr: 500, seed: 42);
            var result = session.TryExecute(new AdvanceTurnCommand());

            Assert.Contains(result.View.FullLog, e => e.Type == "reaction");
        }
    }
}
