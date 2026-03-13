using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for the barrier (energy shield) system:
    /// - Barrier absorbs damage before HP.
    /// - Shield skills grant barrier using the same scaling as damage/heal.
    /// - Barrier decays each round based on the unit's WIS.
    /// - Barrier cannot be healed (HP heal does not restore barrier).
    /// </summary>
    public class BarrierTests
    {
        // ── IsShield property ────────────────────────────────────────────────

        [Fact]
        public void IsShield_ReturnsTrueForShieldSkill()
        {
            var skill = MakeShieldSkill("s1");
            Assert.True(skill.IsShield);
        }

        [Fact]
        public void IsShield_ReturnsFalseForDamageSkill()
        {
            var skill = MakeDamageSkill("d1");
            Assert.False(skill.IsShield);
        }

        [Fact]
        public void IsShield_ReturnsFalseForHealSkill()
        {
            var skill = MakeHealSkill("h1");
            Assert.False(skill.IsShield);
        }

        // ── Barrier absorbs damage before HP ────────────────────────────────
        // Note: PlayerActionCommand auto-advances enemy turns (AutoAdvance inside the handler).
        // After the player casts a shield, the enemy immediately attacks as part of the same call.
        // We read the state after that combined player-shield + enemy-attack action.

        [Fact]
        public void Barrier_AbsorbsDamageBeforeHp()
        {
            // Huge barrier (wisScale=500), tiny damage (wisScale=0.01 → ~4 dmg). HP stays full.
            const int playerMaxHp = 10 * 100;  // Str=10, MaxHp=1000

            var view = CastShieldAndLetEnemyAttack(shieldWisScale: 500.0, damageWisScale: 0.01);
            var playerState = view.Units.First(u => u.UnitId == "player");

            Assert.Equal(playerMaxHp, playerState.CurrentHp);
            Assert.True(playerState.GetBar("barrier") > 0,
                "Barrier should have absorbed the small hit and remain non-zero");
        }

        [Fact]
        public void Barrier_DepletesBeforeHp_WhenDamageExceedsBarrier()
        {
            // Tiny barrier (~4), large damage (~40000). Barrier zeros, HP drops.
            const int playerMaxHp = 10 * 100;

            var view = CastShieldAndLetEnemyAttack(shieldWisScale: 0.01, damageWisScale: 100.0);
            var playerState = view.Units.First(u => u.UnitId == "player");

            Assert.Equal(0, playerState.GetBar("barrier"));
            Assert.True(playerState.CurrentHp < playerMaxHp,
                "HP should be reduced after barrier is depleted");
        }

        [Fact]
        public void Barrier_PartiallyAbsorbsDamage_HpReducedByRemainder()
        {
            // Barrier (~200) < damage (~400): barrier zeroes, HP takes the remainder (~200).
            const int playerMaxHp = 10 * 100;

            var view = CastShieldAndLetEnemyAttack(shieldWisScale: 0.5, damageWisScale: 1.0);
            var playerState = view.Units.First(u => u.UnitId == "player");

            Assert.Equal(0, playerState.GetBar("barrier"));
            Assert.True(playerState.CurrentHp < playerMaxHp,
                "HP should be reduced when damage exceeds barrier");
        }

        // ── Shield skill grants barrier ──────────────────────────────────────

        [Fact]
        public void ShieldSkill_GrantsBarrierToSingleTarget()
        {
            var mage  = new BattleUnit("mage",  "Mage",  "player", Level: 1, Str: 10, Wis: 50, Agi: 100,
                Skills: new BattleSkill[] { MakeShieldSkill("shield") });
            var ally  = new BattleUnit("ally",  "Ally",  "player", Level: 1, Str: 50, Wis: 0,  Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("ally-basic") });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy",  Level: 1, Str: 1,  Wis: 0,  Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { mage, ally },
                EnemyUnits  = new List<BattleUnit> { enemy },
            });

            var result = session.TryExecute(new PlayerActionCommand("shield", "ally"));
            Assert.True(result.Accepted, "Shield skill should be accepted");

            var allyState = result.View.Units.First(u => u.UnitId == "ally");
            Assert.True(allyState.GetBar("barrier") > 0, "Ally should have gained barrier");
        }

        [Fact]
        public void ShieldSkill_AoE_GrantsBarrierToAllAllies()
        {
            var mage  = new BattleUnit("mage",  "Mage",  "player", Level: 1, Str: 10, Wis: 50, Agi: 100,
                Skills: new BattleSkill[] { MakeAoeShieldSkill("shield-aoe") });
            var ally1 = new BattleUnit("ally1", "Ally1", "player", Level: 1, Str: 50, Wis: 0,  Agi: 5,
                Skills: new BattleSkill[] { MakeDamageSkill("a1-basic") });
            var ally2 = new BattleUnit("ally2", "Ally2", "player", Level: 1, Str: 50, Wis: 0,  Agi: 4,
                Skills: new BattleSkill[] { MakeDamageSkill("a2-basic") });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy",  Level: 1, Str: 1,  Wis: 0,  Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { mage, ally1, ally2 },
                EnemyUnits  = new List<BattleUnit> { enemy },
            });

            var result = session.TryExecute(new PlayerActionCommand("shield-aoe", null));
            Assert.True(result.Accepted, "AoE shield skill should be accepted without a target");

            Assert.True(result.View.Units.First(u => u.UnitId == "mage").GetBar("barrier")  > 0, "Mage should have gained barrier");
            Assert.True(result.View.Units.First(u => u.UnitId == "ally1").GetBar("barrier") > 0, "Ally1 should have gained barrier");
            Assert.True(result.View.Units.First(u => u.UnitId == "ally2").GetBar("barrier") > 0, "Ally2 should have gained barrier");
        }

        // ── Barrier decay ────────────────────────────────────────────────────

        [Fact]
        public void Barrier_DecaysEachRound()
        {
            // The shield has a long cooldown so it is only cast once; subsequent mage turns
            // use the damage basic. Enemy is harmless (Wis=0). Rounds pass; barrier decays.
            var session = BuildDecayObservationSession(wisOnShieldedUnit: 0);

            int round1Barrier = session.GetView().Units.First(u => u.UnitId == "shielded").GetBar("barrier");
            Assert.True(round1Barrier > 0, "Sanity: barrier was granted");

            // Advance enough turns for several more round transitions to happen
            for (int i = 0; i < 30; i++)
            {
                session.TryExecute(new AdvanceTurnCommand());
                if (session.GetView().IsOver) break;
            }

            int laterBarrier = session.GetView().Units.First(u => u.UnitId == "shielded").GetBar("barrier");
            Assert.True(laterBarrier < round1Barrier,
                $"Barrier should decrease over rounds (was {round1Barrier}, now {laterBarrier})");
        }

        [Fact]
        public void Barrier_DecaysSlowerWithHighWis()
        {
            int remainingLowWis  = GetBarrierAfterRounds(wisOnShieldedUnit: 0,   rounds: 5);
            int remainingHighWis = GetBarrierAfterRounds(wisOnShieldedUnit: 100, rounds: 5);

            Assert.True(remainingHighWis > remainingLowWis,
                $"High WIS should retain more barrier ({remainingHighWis}) than WIS=0 ({remainingLowWis}) after 5 rounds");
        }

        // ── HP heal does not restore barrier ────────────────────────────────

        [Fact]
        public void HealSkill_DoesNotRestoreBarrier()
        {
            var player = new BattleUnit("player", "Player", "player", Level: 1, Str: 10, Wis: 50, Agi: 100,
                Skills: new BattleSkill[] { MakeShieldSkill("shield", wisScale: 0.1), MakeHealSkill("heal", wisScale: 100.0) });
            var enemy  = new BattleUnit("enemy",  "Enemy",  "enemy",  Level: 1, Str: 10, Wis: 50, Agi: 5,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic", wisScale: 1.0) });

            var session = new BattleSession(seed: 5);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits  = new List<BattleUnit> { enemy },
            });

            // Cast shield. AutoAdvance plays enemy's attack before returning.
            session.TryExecute(new PlayerActionCommand("shield", "player"));
            int barrierBeforeHeal = session.GetView().Units.First(u => u.UnitId == "player").GetBar("barrier");

            // Heal — should not change barrier
            session.TryExecute(new PlayerActionCommand("heal", "player"));
            int barrierAfterHeal = session.GetView().Units.First(u => u.UnitId == "player").GetBar("barrier");

            Assert.Equal(barrierBeforeHeal, barrierAfterHeal);
        }

        // ── Mage-barrier skill loaded from content ───────────────────────────

        [Fact]
        public void MageBarrierSkill_IsLoadedFromContent()
        {
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var skill = db.GetSkill("mage-barrier");
            Assert.NotNull(skill);
            Assert.True(skill.IsShield);
            Assert.True(skill.IsAoe);
            Assert.Equal(4, skill.Cooldown);
        }

        [Fact]
        public void MageUnit_HasBarrierSkill()
        {
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var mage = db.GetUnit("mage");
            Assert.NotNull(mage);
            Assert.Contains(mage.ResolvedSkills, s => s.Id == "mage-barrier");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static SkillEffect[] DamageEffect(double wisScale = 0.01) =>
            new SkillEffect[]
            {
                new(EffectKind.Damage, BattleSkillTarget.Enemy,
                    new DamageComponent[] { new(EffectType.Void, new DamageScaling[] { new("wis", wisScale) }) })
            };

        private static SkillEffect[] ShieldEffect(double wisScale = 1.0) =>
            new SkillEffect[]
            {
                new(EffectKind.Shield, BattleSkillTarget.Ally,
                    new DamageComponent[] { new(null, new DamageScaling[] { new("wis", wisScale) }) })
            };

        private static SkillEffect[] HealEffect(double wisScale = 1.0) =>
            new SkillEffect[]
            {
                new(EffectKind.Heal, BattleSkillTarget.Ally,
                    new DamageComponent[] { new(null, new DamageScaling[] { new("wis", wisScale) }) })
            };

        private static BattleSkill MakeDamageSkill(string id, double wisScale = 0.01) =>
            new(id, id, Cost: 0, DamageMultiplier: 1.0, Effects: DamageEffect(wisScale));

        private static BattleSkill MakeShieldSkill(string id, double wisScale = 1.0) =>
            new(id, id, Cost: 0, DamageMultiplier: 1.0, Effects: ShieldEffect(wisScale));

        private static BattleSkill MakeAoeShieldSkill(string id, double wisScale = 1.0) =>
            new(id, id, Cost: 0, DamageMultiplier: 1.0, IsAoe: true, Effects: ShieldEffect(wisScale));

        private static BattleSkill MakeHealSkill(string id, double wisScale = 1.0) =>
            new(id, id, Cost: 0, DamageMultiplier: 1.0, Effects: HealEffect(wisScale));

        /// <summary>
        /// Casts a shield on a single player (who is also the only target for the enemy),
        /// then reads the state after the enemy attack (which happens inside AutoAdvance).
        /// </summary>
        private static BattleView CastShieldAndLetEnemyAttack(double shieldWisScale, double damageWisScale)
        {
            var player = new BattleUnit("player", "Player", "player", Level: 1, Str: 10, Wis: 50, Agi: 100,
                Skills: new BattleSkill[] { MakeShieldSkill("shield", shieldWisScale) });
            var enemy  = new BattleUnit("enemy",  "Enemy",  "enemy",  Level: 1, Str: 10, Wis: 50, Agi: 5,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic", damageWisScale) });

            var session = new BattleSession(seed: 42);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits  = new List<BattleUnit> { enemy },
            });

            // PlayerActionCommand auto-advances enemy turns (AutoAdvance) before returning.
            // After this call: player shielded themselves, enemy attacked, round ended.
            var result = session.TryExecute(new PlayerActionCommand("shield", "player"));
            return result.View;
        }

        /// <summary>
        /// Builds a session where a mage (agi=100) casts a large one-time barrier on all allies
        /// (the shield has a 100-turn cooldown so it's cast exactly once) and then uses a basic
        /// attack for subsequent turns. Runs until the first round transition so barrier has
        /// already decayed once when the session is returned.
        /// </summary>
        private static BattleSession BuildDecayObservationSession(int wisOnShieldedUnit)
        {
            // Shield has Cooldown=100 so it's only usable on the first turn.
            var oneTimeShield = new BattleSkill("shield", "Shield",
                Cost: 0, DamageMultiplier: 1.0, Cooldown: 100, IsAoe: true,
                Effects: ShieldEffect(wisScale: 500.0));

            var mage     = new BattleUnit("mage",     "Mage",     "player",
                Level: 1, Str: 10, Wis: 50, Agi: 100,
                Skills: new BattleSkill[] { MakeDamageSkill("mage-basic"), oneTimeShield });
            var shielded = new BattleUnit("shielded", "Shielded", "player",
                Level: 1, Str: 50, Wis: wisOnShieldedUnit, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("s-basic") });
            var enemy    = new BattleUnit("enemy",    "Enemy",    "enemy",
                Level: 1, Str: 1, Wis: 0, Agi: 2,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 99);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { mage, shielded },
                EnemyUnits  = new List<BattleUnit> { enemy },
            });

            // Mage casts AoE barrier (once — then it's on 100-turn cooldown).
            // AutoAdvance plays enemy and shielded turns, completing round 1.
            session.TryExecute(new PlayerActionCommand("shield", null));

            return session;
        }

        private int GetBarrierAfterRounds(int wisOnShieldedUnit, int rounds)
        {
            var session = BuildDecayObservationSession(wisOnShieldedUnit);

            int roundsCompleted = 0;
            for (int i = 0; i < 200 && roundsCompleted < rounds && !session.GetView().IsOver; i++)
            {
                int roundBefore = session.GetView().Round;
                session.TryExecute(new AdvanceTurnCommand());
                if (session.GetView().Round > roundBefore) roundsCompleted++;
            }

            return session.GetView().Units.First(u => u.UnitId == "shielded").GetBar("barrier");
        }
    }
}
