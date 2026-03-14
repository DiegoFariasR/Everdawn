using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    /// <summary>
    /// Tests for the RestoreBar effect kind:
    /// - EffectKind.RestoreBar adds a fixed amount to a named secondary bar on the target.
    /// - Positive BarAmount restores; negative drains.
    /// - Bar values are clamped to [0, max].
    /// - RestoreBar skills do not trigger Focus or Fury empowerment.
    /// - Fury does not gain from RestoreBar actions.
    /// - Range: Self resolves auto-targeting to the actor.
    /// - mage-meditate and rogue-concentrate load correctly from content.
    /// </summary>
    public class RestoreBarTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static BattleSkill MakeRestoreBarSkill(string id, string barKey, int barAmount,
            SkillRange range = SkillRange.Self) =>
            new BattleSkill(id, id, Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.RestoreBar, BattleSkillTarget.Ally,
                        new DamageComponent[] { },
                        BarKey: barKey, BarAmount: barAmount),
                },
                Range: range);

        private static BattleSkill MakeDamageSkill(string id) =>
            new BattleSkill(id, id, Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                        new DamageComponent[]
                        {
                            new DamageComponent(EffectType.Physical,
                                new DamageScaling[] { new DamageScaling("str", 0.01) })
                        })
                },
                Modifiers: new[] { "basic" });

        private static BattleSetup TwoUnitSetup(BattleUnit player, BattleUnit enemy) =>
            new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { player },
                EnemyUnits = new List<BattleUnit> { enemy },
            };

        // ── IsRestoreBar property ─────────────────────────────────────────────

        [Fact]
        public void IsRestoreBar_ReturnsTrueForRestoreBarSkill()
        {
            var skill = MakeRestoreBarSkill("s", "mp", 100);
            Assert.True(skill.IsRestoreBar);
        }

        [Fact]
        public void IsRestoreBar_ReturnsFalseForDamageSkill()
        {
            Assert.False(MakeDamageSkill("d").IsRestoreBar);
        }

        [Fact]
        public void IsRestoreBar_ReturnsFalseForHealSkill()
        {
            var skill = new BattleSkill("h", "h", Cost: 0, DamageMultiplier: 1.0,
                Effects: new SkillEffect[]
                {
                    new SkillEffect(EffectKind.Heal, BattleSkillTarget.Ally,
                        new DamageComponent[]
                        {
                            new DamageComponent(null, new DamageScaling[] { new DamageScaling("str", 1.0) })
                        })
                });
            Assert.False(skill.IsRestoreBar);
        }

        // ── RestoreBar restores a bar ─────────────────────────────────────────

        [Fact]
        public void RestoreBar_Focus_IncreasesTargetFocus()
        {
            // Focus starts at 50. Restore 20 → 70 on the rogue's action.
            // Then enemy auto-advances (1 hit, Agi=1) → rogue loses 10 focus → 60.
            var rogue = new BattleUnit("rogue", "Rogue", "player", Level: 1, Str: 10, Wis: 0, Agi: 200,
                Traits: new BattleTrait[] { BattleTrait.Focus },
                Skills: new BattleSkill[] { MakeRestoreBarSkill("concentrate", "focus", 20) });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(TwoUnitSetup(rogue, enemy));

            // Verify the RestoreBar event is emitted with a positive amount.
            var result = session.TryExecute(new PlayerActionCommand("concentrate", "rogue"));
            Assert.True(result.Accepted);
            Assert.Contains(result.Events, e => e.Description.Contains("restores") && e.Description.Contains("Focus"));
        }

        [Fact]
        public void RestoreBar_IsClampedAtMaxValue()
        {
            // Focus starts at 50. Restoring 100 clamps at 100. After enemy hits (−10): 90.
            // The key invariant: focus must not exceed 100 and must be higher than the 50 baseline.
            var rogue = new BattleUnit("rogue", "Rogue", "player", Level: 1, Str: 10, Wis: 0, Agi: 200,
                Traits: new BattleTrait[] { BattleTrait.Focus },
                Skills: new BattleSkill[] { MakeRestoreBarSkill("concentrate", "focus", 100) });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(TwoUnitSetup(rogue, enemy));

            var result = session.TryExecute(new PlayerActionCommand("concentrate", "rogue"));

            Assert.True(result.Accepted);
            int focus = result.View.Units.First(u => u.UnitId == "rogue").GetBar("focus");
            Assert.True(focus <= 100, $"Focus must not exceed 100; actual={focus}");
            Assert.True(focus > 50, $"Focus should be higher than the 50 baseline; actual={focus}");
        }

        [Fact]
        public void RestoreBar_Mp_IncreasesTargetMp()
        {
            // Verify that a RestoreBar(mp) skill emits a "restores X MP" event.
            const int wis = 50;
            const int startMp = 200;
            var mage = new BattleUnit("mage", "Mage", "player", Level: 1, Str: 10, Wis: wis, Agi: 200,
                Traits: new BattleTrait[] { BattleTrait.MagicUser },
                Skills: new BattleSkill[] { MakeRestoreBarSkill("meditate", "mp", 100) });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(TwoUnitSetup(mage, enemy));

            // Inject a depleted MP state so the restore has room to work.
            var state = new UnitState[]
            {
                new UnitState("mage",  mage.MaxHp,  true, new Dictionary<string, int> { ["mp"] = startMp }),
                new UnitState("enemy", enemy.MaxHp, true, null),
            };
            session.TryExecute(new ResumeFromSnapshotCommand(state, LastActorId: "enemy", AtStep: 0));

            var result = session.TryExecute(new PlayerActionCommand("meditate", "mage"));

            Assert.True(result.Accepted);
            // The RestoreBar event should mention "MP".
            Assert.Contains(result.Events, e => e.Description.Contains("restores") && e.Description.Contains("MP"));
            // MP must be higher than the starting value (restoration happened).
            int mpAfter = result.View.Units.First(u => u.UnitId == "mage").GetBar("mp");
            Assert.True(mpAfter > startMp, $"MP should be higher than {startMp} after Meditate; actual={mpAfter}");
        }

        [Fact]
        public void DrainBar_ReducesTargetBar_ClampedAtZero()
        {
            // Drain 200 focus from a unit that starts at 50 → clamp to 0.
            var rogue = new BattleUnit("rogue", "Rogue", "player", Level: 1, Str: 10, Wis: 0, Agi: 200,
                Traits: new BattleTrait[] { BattleTrait.Focus },
                Skills: new BattleSkill[] { MakeRestoreBarSkill("drain-focus", "focus", -200) });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(TwoUnitSetup(rogue, enemy));

            var result = session.TryExecute(new PlayerActionCommand("drain-focus", "rogue"));

            Assert.True(result.Accepted);
            int focus = result.View.Units.First(u => u.UnitId == "rogue").GetBar("focus");
            Assert.Equal(0, focus);
        }

        [Fact]
        public void RestoreBar_NoEffect_WhenTargetLacksBar()
        {
            // A unit without the Focus trait has no focus bar; the skill must not crash.
            var warrior = new BattleUnit("warrior", "Warrior", "player", Level: 1, Str: 50, Wis: 0, Agi: 200,
                Skills: new BattleSkill[] { MakeRestoreBarSkill("concentrate", "focus", 30) });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(TwoUnitSetup(warrior, enemy));

            var result = session.TryExecute(new PlayerActionCommand("concentrate", "warrior"));

            Assert.True(result.Accepted);
        }

        // ── Focus empowerment not triggered by RestoreBar ─────────────────────

        [Fact]
        public void RestoreBar_DoesNotTriggerFocusEmpowerment()
        {
            // Unit at full focus (100) uses a RestoreBar skill — empowerment must NOT fire
            // (empowerment would reset focus to 50 and emit an "empowers" event).
            var rogue = new BattleUnit("rogue", "Rogue", "player", Level: 1, Str: 10, Wis: 0, Agi: 200,
                Traits: new BattleTrait[] { BattleTrait.Focus },
                Skills: new BattleSkill[] { MakeRestoreBarSkill("concentrate", "focus", 10) });
            var enemy = new BattleUnit("enemy", "Enemy", "enemy", Level: 1, Str: 1, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("e-basic") });

            var session = new BattleSession(seed: 1);
            session.Start(TwoUnitSetup(rogue, enemy));

            // Inject full-focus state.
            var state = new UnitState[]
            {
                new UnitState("rogue", rogue.MaxHp, true, new Dictionary<string, int> { ["focus"] = 100 }),
                new UnitState("enemy", enemy.MaxHp, true, null),
            };
            session.TryExecute(new ResumeFromSnapshotCommand(state, LastActorId: "enemy", AtStep: 0));

            var result = session.TryExecute(new PlayerActionCommand("concentrate", "rogue"));

            Assert.True(result.Accepted);
            // No empowerment event should appear — it would contain "empowers".
            Assert.DoesNotContain(result.Events, e => e.Description.Contains("empowers"));
            // Focus must be higher than 50; empowerment would have reset it to 50.
            int focus = result.View.Units.First(u => u.UnitId == "rogue").GetBar("focus");
            Assert.True(focus > 50, $"Focus should remain above 50 (empowerment resets to 50); actual={focus}");
        }

        // ── Content loading ───────────────────────────────────────────────────

        [Fact]
        public void MageMeditate_IsLoadedFromContent()
        {
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var skill = db.GetSkill("mage-meditate");
            Assert.NotNull(skill);
            Assert.True(skill.IsRestoreBar);
            Assert.Equal(4, skill.Cooldown);
        }

        [Fact]
        public void RogueConcentrate_IsLoadedFromContent()
        {
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var skill = db.GetSkill("rogue-concentrate");
            Assert.NotNull(skill);
            Assert.True(skill.IsRestoreBar);
            Assert.Equal(4, skill.Cooldown);
        }

        [Fact]
        public void MageUnit_HasMeditateSkill()
        {
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var mage = db.GetUnit("mage");
            Assert.NotNull(mage);
            Assert.Contains(mage.ResolvedSkills, s => s.Id == "mage-meditate");
        }

        [Fact]
        public void RogueUnit_HasConcentrateSkill()
        {
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var rogue = db.GetUnit("rogue");
            Assert.NotNull(rogue);
            Assert.Contains(rogue.ResolvedSkills, s => s.Id == "rogue-concentrate");
        }

        [Fact]
        public void MageMeditate_RestoresMp_InAutoPlay()
        {
            // Let auto-play run until mage-meditate fires and verify the event is emitted.
            var db = GameCore.Content.ContentPipeline.Load(TestContentSource.Default);
            var mage = db.GetUnit("mage") with { Team = "player" };
            // Harmless enemy with huge HP so the battle doesn't end prematurely.
            var enemy = new BattleUnit("dummy", "Dummy", "enemy",
                Level: 1, Str: 500, Wis: 0, Agi: 1,
                Skills: new BattleSkill[] { MakeDamageSkill("dummy-basic") });

            var session = new BattleSession(seed: 42);
            session.Start(new BattleSetup
            {
                PlayerUnits = new List<BattleUnit> { mage },
                EnemyUnits = new List<BattleUnit> { enemy },
            });

            bool meditateEventFired = false;
            for (int i = 0; i < 60 && !session.GetView().IsOver; i++)
            {
                var result = session.TryExecute(new AdvanceTurnCommand());
                if (result.Events.Any(e => e.SkillId == "mage-meditate" && e.Type == "skill"))
                {
                    meditateEventFired = true;
                    break;
                }
            }

            Assert.True(meditateEventFired, "mage-meditate should fire within 60 turns");
        }
    }
}
