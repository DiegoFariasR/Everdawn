using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class BattleTraitTests
    {
        // ── HasTrait ──────────────────────────────────────────────────────────

        [Fact]
        public void HasTrait_UnitWithTrait_ReturnsTrue()
        {
            var unit = MakeUnit(traits: new[] { BattleTrait.MagicUser });
            Assert.True(unit.HasTrait(BattleTrait.MagicUser));
        }

        [Fact]
        public void HasTrait_UnitWithoutTraits_ReturnsFalse()
        {
            var unit = MakeUnit(traits: null);
            Assert.False(unit.HasTrait(BattleTrait.MagicUser));
        }

        [Fact]
        public void HasTrait_EmptyTraitList_ReturnsFalse()
        {
            var unit = MakeUnit(traits: new BattleTrait[0]);
            Assert.False(unit.HasTrait(BattleTrait.MagicUser));
        }

        // ── MagicUser — MaxMp ─────────────────────────────────────────────────

        [Fact]
        public void MagicUser_MaxMp_IsDerivedFromWis()
        {
            var unit = MakeUnit(wis: 100, traits: new[] { BattleTrait.MagicUser });
            Assert.Equal(1000, unit.MaxBars.TryGetValue("mp", out int v) ? v : 0);
        }

        [Fact]
        public void MagicUser_MaxMp_IgnoresMaxMpOverride()
        {
            var unit = MakeUnit(wis: 100, maxMpOverride: 999, traits: new[] { BattleTrait.MagicUser });
            Assert.Equal(1000, unit.MaxBars.TryGetValue("mp", out int v) ? v : 0);  // WIS * 10, not the override
        }

        [Fact]
        public void NoTrait_MaxMp_UsesMaxMpOverride()
        {
            var unit = MakeUnit(wis: 100, maxMpOverride: 50, traits: null);
            Assert.Equal(50, unit.MaxBars.TryGetValue("mp", out int v) ? v : 0);
        }

        [Fact]
        public void NoTrait_NoOverride_MaxMpIsZero()
        {
            var unit = MakeUnit(wis: 100, traits: null);
            Assert.False(unit.MaxBars.ContainsKey("mp"));
        }

        // ── Sample scenario units ─────────────────────────────────────────────

        [Fact]
        public void Mage_WithMagicUserTrait_HasPositiveMaxMp()
        {
            var mage = MakeUnit(wis: 110, traits: new[] { BattleTrait.MagicUser });
            Assert.Equal(1100, mage.MaxBars.TryGetValue("mp", out int v) ? v : 0);
        }

        [Fact]
        public void Necromancer_WithMagicUserTrait_HasPositiveMaxMp()
        {
            var necro = MakeUnit(wis: 105, traits: new[] { BattleTrait.MagicUser });
            Assert.Equal(1050, necro.MaxBars.TryGetValue("mp", out int v) ? v : 0);
        }
        // ── Multiple traits ────────────────────────────────────────────────────

        [Fact]
        public void Unit_WithMultipleTraits_HasTraitReturnsTrue_ForEach()
        {
            var unit = MakeUnit(traits: new[] { BattleTrait.MagicUser, BattleTrait.Focus });
            Assert.True(unit.HasTrait(BattleTrait.MagicUser));
            Assert.True(unit.HasTrait(BattleTrait.Focus));
        }

        [Fact]
        public void Unit_WithMultipleTraits_BothDerivedStatsAreActive()
        {
            // MagicUser → MaxMp = WIS × 10. Focus → MaxFocus = 100, InitialFocus = 100.
            var unit = MakeUnit(wis: 100, traits: new[] { BattleTrait.MagicUser, BattleTrait.Focus });
            Assert.Equal(1000, unit.MaxBars.TryGetValue("mp", out int mp) ? mp : 0);
            Assert.Equal(100, unit.MaxBars.TryGetValue("focus", out int maxF) ? maxF : 0);
            Assert.Equal(100, unit.InitialBars.TryGetValue("focus", out int initF) ? initF : 0);
        }

        [Fact]
        public void Unit_WithMultipleTraits_InBattle_BothBarsInitialise()
        {
            // A unit with both MagicUser and Focus must start with both mana and focus.
            var setup = new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    new("hero", "Hero", "player", Level: 1, Str: 200, Wis: 100, Agi: 50,
                        Skills: new BattleSkill[] { new("basic", "Strike", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect()) },
                        Traits: new[] { BattleTrait.MagicUser, BattleTrait.Focus }),
                },
                EnemyUnits = new List<BattleUnit>
                {
                    new("dummy", "Dummy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                        Skills: new BattleSkill[] { new("e-basic", "Hit", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect()) }),
                },
            };
            var session = new BattleSession(seed: 0);
            session.Start(setup);
            var state = session.GetView().Units.First(u => u.UnitId == "hero");
            // MagicUser → MaxMp = WIS × 10 = 1000; starts at full
            Assert.Equal(1000, state.GetBar("mp"));
            // Focus → starts at 100 (full)
            Assert.Equal(100, state.GetBar("focus"));
        }
        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Minimal physical/str effect list for test skill construction.</summary>
        private static SkillEffect[] PhysEffect(double mult = 1.0) =>
            new SkillEffect[] { new(EffectKind.Damage, BattleSkillTarget.Enemy,
                new DamageComponent[] { new(EffectType.Physical, new DamageScaling[] { new("str", mult) }) }) };

        private static BattleUnit MakeUnit(
            int wis = 50,
            int maxMpOverride = 0,
            IReadOnlyList<BattleTrait>? traits = null) =>
            new("unit", "Unit", "player", Level: 1, Str: 50, Wis: wis, Agi: 50,
                MaxMpOverride: maxMpOverride, Traits: traits);
    }
}
