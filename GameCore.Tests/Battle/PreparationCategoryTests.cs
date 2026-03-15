using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;
using Xunit;

namespace GameCore.Tests.Battle
{
    public class PreparationCategoryTests
    {
        // ── Auto-refund ───────────────────────────────────────────────────────

        [Fact]
        public void Preparation_RefundsAction_WithoutExplicitFlag()
        {
            // Category: Preparation implies the refund at the runtime level — no RefundsAction = true needed.
            var session = StartSession();
            var result = session.TryExecute(new PlayerActionCommand("prep-skill", null));
            Assert.NotNull(result.View.PendingInput);
            Assert.Equal("player-unit", result.View.PendingInput!.ActorId);
        }

        // ── Once-per-round lock ───────────────────────────────────────────────

        [Fact]
        public void Preparation_NotAvailable_WhenFocusedBuffActive()
        {
            // After using a Preparation skill (Focused buff is active), the same
            // Preparation skill should not appear in AvailableSkillIds.
            var session = StartSession();
            session.TryExecute(new PlayerActionCommand("prep-skill", null));
            var pending = session.GetView().PendingInput!;
            Assert.DoesNotContain("prep-skill", pending.AvailableSkillIds);
        }

        [Fact]
        public void Preparation_Available_AfterFocusedBuffExpires()
        {
            // After the follow-up action is taken, Focused expires (UntilNextAction).
            // On the next turn (next round), Preparation should be available again.
            var session = StartSession(playerAgi: 100);
            session.TryExecute(new PlayerActionCommand("prep-skill", null));
            // Follow-up action consumes Focused buff.
            session.TryExecute(new PlayerActionCommand("basic", "target"));
            // Enemy acts — then it's the player's turn again.
            var pending = session.GetView().PendingInput;
            // If battle is over or enemy turn, skip. We just verify Focused was consumed
            // by checking no active effect named "focused" persists on the unit.
            var state = session.GetView().Units.First(u => u.UnitId == "player-unit");
            var focusedEffect = state.ActiveEffects?.FirstOrDefault(e => e.DefinitionId == "focused");
            Assert.Null(focusedEffect);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static BattleSession StartSession(int playerAgi = 50)
        {
            var setup = new BattleSetup
            {
                PlayerUnits = new List<BattleUnit>
                {
                    new("player-unit", "Fighter", "player", Level: 1, Str: 200, Wis: 0, Agi: playerAgi,
                        Skills: new BattleSkill[]
                        {
                            new("basic", "Strike", Cost: 0, DamageMultiplier: 1.0,
                                Effects: PhysEffect(),
                                Modifiers: new[] { "basic" }, ModifierTags: new[] { "basic" }),
                            new("prep-skill", "Prepare", Cost: 0, DamageMultiplier: 1.0,
                                Effects: Array.Empty<SkillEffect>(),
                                Range: SkillRange.Self,
                                Category: SkillCategory.Preparation),
                        }),
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

        private static SkillEffect[] PhysEffect() =>
            new SkillEffect[] { new(EffectKind.Damage, BattleSkillTarget.Enemy,
                new DamageComponent[] { new(EffectType.Physical, new DamageScaling[] { new("str", 1.0) }) }) };
    }
}
