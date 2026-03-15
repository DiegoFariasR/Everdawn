using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class FocusTraitTests
    {
        // ── BattleUnit derived props ──────────────────────────────────────────

        [Fact]
        public void Focus_MaxFocus_IsOneHundred()
        {
            var unit = MakeUnit(traits: new[] { BattleTrait.FocusUser });
            Assert.Equal(100, unit.MaxBars.TryGetValue("focus", out int v) ? v : 0);
        }

        [Fact]
        public void Focus_InitialFocus_IsOneHundred()
        {
            // Focus starts at full so units can immediately use the Focus skill.
            var unit = MakeUnit(traits: new[] { BattleTrait.FocusUser });
            Assert.Equal(100, unit.InitialBars.TryGetValue("focus", out int v) ? v : 0);
        }

        [Fact]
        public void NoTrait_MaxFocus_IsZero()
        {
            var unit = MakeUnit(traits: null);
            Assert.False(unit.MaxBars.ContainsKey("focus"));
        }

        [Fact]
        public void NoTrait_InitialFocus_IsZero()
        {
            var unit = MakeUnit(traits: null);
            Assert.False(unit.InitialBars.ContainsKey("focus"));
        }

        // ── Initial state ─────────────────────────────────────────────────────

        [Fact]
        public void FocusUnit_StartsAtFullFocus()
        {
            // Player goes first (Agi 50 > enemy Agi 1) — enemy never acts before the initial view.
            var session = StartFocusBattle(playerAgi: 50);
            var state = session.GetView().Units.First(u => u.UnitId == "focus-unit");
            Assert.Equal(100, state.GetBar("focus"));
        }

        // ── Focus skill: spend focus, grant Focused buff, refund action ────────

        [Fact]
        public void FocusSkill_SpendsFocusCost()
        {
            // Using the Focus skill deducts FocusCost (100) from the bar.
            var session = StartFocusBattle();
            session.TryExecute(new PlayerActionCommand("focus-skill", null));
            var state = session.GetView().Units.First(u => u.UnitId == "focus-unit");
            Assert.Equal(0, state.GetBar("focus"));
        }

        [Fact]
        public void FocusSkill_EmitsFocusedEvent()
        {
            var session = StartFocusBattle();
            var result = session.TryExecute(new PlayerActionCommand("focus-skill", null));
            Assert.Contains(result.Events, e => e.Description.Contains("focuses intently"));
        }

        [Fact]
        public void FocusSkill_RefundsAction_PlayerStillActive()
        {
            // After using the Focus skill the turn is not consumed — PendingInput still present.
            var session = StartFocusBattle();
            var result = session.TryExecute(new PlayerActionCommand("focus-skill", null));
            Assert.NotNull(result.View.PendingInput);
            Assert.Equal("focus-unit", result.View.PendingInput!.ActorId);
        }

        [Fact]
        public void FocusSkill_NotAvailable_WhenInsufficientFocus()
        {
            // Inject focus = 0 so the Focus skill (cost 100) is absent from AvailableSkillIds.
            var session = StartFocusBattle();
            var snapshotState = new[]
            {
                new UnitState("focus-unit", 200 * 100, true, new Dictionary<string, int> { ["focus"] = 0 }),
                new UnitState("target", 1 * 100, true, null),
            };
            session.TryExecute(new ResumeFromSnapshotCommand(snapshotState, LastActorId: "target", AtStep: 0));
            var pending = session.GetView().PendingInput!;
            Assert.DoesNotContain("focus-skill", pending.AvailableSkillIds);
        }

        // ── Focused buff: consumed by the first compatible skill ──────────────

        [Fact]
        public void Focused_OnCompatibleSkill_AddsExtraHit()
        {
            // Player Agi 100 → HitCount = 2. After Focus, compatible skill gets +1 hit = 3 total.
            // We verify via TotalHits on the first damage event (set before the hit loop), so the
            // test is not sensitive to whether the target survives all hits.
            var session = StartFocusBattle(playerAgi: 100);

            // Step 1: use Focus skill (refunded action).
            session.TryExecute(new PlayerActionCommand("focus-skill", null));

            // Step 2: use compatible skill — Focused consumed, +1 hit applied.
            var result = session.TryExecute(new PlayerActionCommand("compatible", "target"));

            // TotalHits on any damage event reflects effectiveHits at time of the action.
            var hit = result.Events.FirstOrDefault(e => e.TargetId == "target" && e.Value > 0);
            Assert.NotNull(hit);
            Assert.Equal(3, hit!.TotalHits);
        }

        [Fact]
        public void Focused_OnCompatibleSkill_EmitsSharpenEvent()
        {
            var session = StartFocusBattle();
            session.TryExecute(new PlayerActionCommand("focus-skill", null));
            var result = session.TryExecute(new PlayerActionCommand("compatible", "target"));
            Assert.Contains(result.Events, e => e.Description.Contains("sharpens"));
        }

        [Fact]
        public void Focused_OnIncompatibleSkill_NoSharpenEvent()
        {
            // Using an incompatible skill during the refunded action should not trigger Focus empowerment.
            var session = StartFocusBattle();
            session.TryExecute(new PlayerActionCommand("focus-skill", null));
            var result = session.TryExecute(new PlayerActionCommand("basic", "target"));
            Assert.DoesNotContain(result.Events, e => e.Description.Contains("sharpens"));
        }

        [Fact]
        public void Focused_OnIncompatibleSkill_NoExtraHit_BaseHitCount()
        {
            // Player Agi 50 → HitCount = 1. Basic skill is incompatible → no extra hit.
            var session = StartFocusBattle(playerAgi: 50);
            session.TryExecute(new PlayerActionCommand("focus-skill", null));
            var result = session.TryExecute(new PlayerActionCommand("basic", "target"));
            int damageEvents = result.Events.Count(e => e.TargetId == "target" && e.Value > 0);
            Assert.Equal(1, damageEvents);
        }

        // ── Focus regen ───────────────────────────────────────────────────────

        [Fact]
        public void Focus_RegensPerTurn()
        {
            // Inject focus = 0 via Resume, then player takes an action.
            // StartOfTurn fires and adds FocusRegenPerTurn (20) before the action.
            var session = StartFocusBattle(playerAgi: 50);
            var snapshotState = new[]
            {
                new UnitState("focus-unit", 200 * 100, true, new Dictionary<string, int> { ["focus"] = 0 }),
                new UnitState("target", 1 * 100, true, null),
            };
            session.TryExecute(new ResumeFromSnapshotCommand(snapshotState, LastActorId: "target", AtStep: 0));

            // Player uses basic (non-Focus skill) — StartOfTurn adds 20 focus.
            session.TryExecute(new PlayerActionCommand("basic", "target"));

            // Battle ends (player one-shots the enemy), but we can check the post-action view.
            // Focus = 0 (start) + 20 (regen) = 20. No other focuses costs were paid.
            var state = session.GetView().Units.First(u => u.UnitId == "focus-unit");
            Assert.Equal(20, state.GetBar("focus"));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Player: basic (incompatible), compatible (ExtraHit +1, IsFocusCompatible), focus-skill. playerAgi controls HitCount (1 + Agi/100).
        private static BattleSession StartFocusBattle(int playerAgi = 50)
        {
            var setup = new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    new("focus-unit", "Fighter", "player", Level: 1, Str: 200, Wis: 0, Agi: playerAgi,
                        Skills: new BattleSkill[]
                        {
                            new("basic",      "Strike",      Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect(),
                                Modifiers: new[] { "basic" }, ModifierTags: new[] { "basic" }),
                            new("compatible", "Power Strike", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect(),
                                IsFocusCompatible: true, FocusEffect: FocusEffectKind.ExtraHit, FocusEffectValue: 1),
                            new("focus-skill", "Focus",       Cost: 100, DamageMultiplier: 1.0,
                                Effects: new SkillEffect[] { new(EffectKind.GrantFocusedBuff, BattleSkillTarget.Ally, Array.Empty<DamageComponent>()) },
                                Range: SkillRange.Self, Category: SkillCategory.Preparation,
                                PermittedTraits: new[] { BattleTrait.FocusUser }),
                        },
                        Traits: new[] { BattleTrait.FocusUser }),
                },
                EnemyUnits = new List<BattleUnit>
                {
                    new("target", "Dummy", "enemy", Level: 1, Str: 10, Wis: 0, Agi: 1,
                        Skills: new BattleSkill[] { new("def-basic", "Slash", Cost: 0, DamageMultiplier: 1.0, Effects: PhysEffect()) }),
                },
            };
            var session = new BattleSession(seed: 0);
            session.Start(setup);
            return session;
        }

        private static BattleUnit MakeUnit(IReadOnlyList<BattleTrait>? traits) =>
            new("unit", "Unit", "player", Level: 1, Str: 50, Wis: 50, Agi: 50, Traits: traits);

        // Minimal physical/str effect list for test skill construction.
        private static SkillEffect[] PhysEffect(double mult = 1.0) =>
            new SkillEffect[] { new(EffectKind.Damage, BattleSkillTarget.Enemy,
                new DamageComponent[] { new(EffectType.Physical, new DamageScaling[] { new("str", mult) }) }) };
    }
}
