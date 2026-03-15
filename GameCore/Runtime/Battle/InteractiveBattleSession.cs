#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
    // Internal battle runner. All external access goes through BattleSession : IBattleEngine.
    internal sealed class InteractiveBattleSession
    {
        private readonly List<BattleUnit> _allUnits;
        private readonly List<BattleUnit> _turnOrder;
        private readonly Dictionary<string, int> _hp;
        // _bars[unitId][barKey] = current value — unified store for MP, Focus, Fury, and any future bars.
        private readonly Dictionary<string, Dictionary<string, int>> _bars;
        private readonly Random _rng;
        private readonly List<BattleEvent> _log = new List<BattleEvent>();

        private int _turnIndex;
        private int _round = 1;
        private bool _started;
        private readonly Dictionary<string, int> _skillCooldowns = new();
        private bool _isOver;
        private string? _winningTeam;
        // Units that will lose their next turn due to the stunned status effect.
        private readonly HashSet<string> _stunnedUnits = new HashSet<string>();
        // Active runtime effects: unitId → list of instances currently on that unit.
        private readonly Dictionary<string, List<ActiveEffectInstance>> _activeEffects =
            new Dictionary<string, List<ActiveEffectInstance>>();
        // Counter for generating unique runtime instance IDs within this session.
        private int _nextEffectId;
        // Thermal status definitions (slow, frozen, burning, chilled, heated). Loaded from content or created as fallbacks.
        private readonly ActiveEffectDefinition _slowDef;
        private readonly ActiveEffectDefinition _frozenDef;
        private readonly ActiveEffectDefinition _burningDef;
        private readonly ActiveEffectDefinition _chilledDef;
        private readonly ActiveEffectDefinition _heatedDef;
        private readonly ActiveEffectDefinition _bleedingDef;
        private readonly ActiveEffectDefinition _scratchedDef;
        // Units that currently carry the Focused buff (granted by the Focus skill).
        // Focused is consumed by the first IsFocusCompatible skill the unit uses, or expires when their turn ends.
        private readonly HashSet<string> _focusedUnits = new HashSet<string>();
        // Set by ExecuteAction when a skill has RefundsAction=true. Callers must skip AdvanceTurn for this action.
        private bool _actionRefunded;
        // Set after StartOfTurn runs for the current _turnIndex.
        // Prevents re-applying burn DOT and bar decay on the free follow-up action granted by Focus.
        private bool _startOfTurnDone;
        // Focus points regenerated at the start of each turn for units with a Focus bar.
        private const int FocusRegenPerTurn = 20;

        public InteractiveBattleSession(BattleSetup setup, int seed)
        {
            _allUnits = new List<BattleUnit>(setup.PlayerUnits.Concat(setup.EnemyUnits));
            _turnOrder = new List<BattleUnit>(_allUnits.OrderByDescending(u => u.Initiative));
            _hp = _allUnits.ToDictionary(u => u.Id, u => u.MaxHp);
            _bars = _allUnits.ToDictionary(u => u.Id, u => new Dictionary<string, int>(u.InitialBars));
            _rng = new Random(seed);
            // Apply initial cooldowns (Ultimate modifier auto-supplies 1 round if not set higher)
            foreach (var u in _allUnits)
            {
                foreach (var s in u.ResolvedSkills)
                    if (s.EffectiveInitialCooldown > 0)
                        _skillCooldowns[s.Id] = s.EffectiveInitialCooldown;
                // Reaction skills have their own CD slot.
                if (u.ReactionSkill?.EffectiveInitialCooldown > 0)
                    _skillCooldowns[u.ReactionSkill.Id] = u.ReactionSkill.EffectiveInitialCooldown;
            }
            // Load thermal buff definitions from content, falling back to minimal inline definitions
            // so tests that omit BuffDefinitions still work.
            _slowDef = ResolveThermalDef(setup, ThermalSystem.StatusSlow, "Slow");
            _frozenDef = ResolveThermalDef(setup, ThermalSystem.StatusFrozen, "Frozen");
            _burningDef = ResolveThermalDef(setup, ThermalSystem.StatusBurning, "Burning");
            _chilledDef = ResolveThermalDef(setup, ThermalSystem.StatusChilled, "Chilled");
            _heatedDef = ResolveThermalDef(setup, ThermalSystem.StatusHeated, "Heated");
            _bleedingDef = ResolveThermalDef(setup, BleedSystem.StatusBleeding, "Bleeding");
            _scratchedDef = ResolveThermalDef(setup, BleedSystem.StatusScratched, "Scratched");
        }

        private static ActiveEffectDefinition ResolveThermalDef(
            BattleSetup setup, string id, string fallbackName)
        {
            if (setup.BuffDefinitions != null
                && setup.BuffDefinitions.TryGetValue(id, out var def))
                return def;
            return new ActiveEffectDefinition(
                id, fallbackName, EffectDurationKind.Permanent, 0,
                EffectStackingPolicy.RefreshDuration);
        }

        /// <summary>
        /// Process a client request and return the resulting battle state.
        /// The returned <see cref="BattleResponse"/> is the complete picture —
        /// clients must not query session internals directly.
        /// </summary>
        public BattleResponse HandleRequest(BattleRequest request) => request switch
        {
            InitiateBattleRequest r => HandleStart(r),
            ResumeFromSnapshotRequest r => HandleResume(r),
            PlayerActionRequest r => HandlePlayerAction(r),
            AutoPlayerActionRequest r => HandleAutoPlayerAction(r),
            AdvanceOneTurnRequest r => HandleAdvanceOneTurn(r),
            _ => throw new ArgumentException($"Unknown request type: {request.GetType().Name}")
        };

        // ── Request handlers ─────────────────────────────────────────────────

        private BattleResponse HandleStart(InitiateBattleRequest _)
        {
            if (_started) throw new InvalidOperationException("Session already started.");
            _started = true;
            AddEvent("system", "Battle begins!", "start");
            return BuildResponse(AutoAdvance());
        }

        private BattleResponse HandleResume(ResumeFromSnapshotRequest r)
        {
            if (_started) throw new InvalidOperationException("Session already started.");
            _started = true;

            foreach (var us in r.State)
            {
                if (_hp.ContainsKey(us.UnitId)) _hp[us.UnitId] = us.CurrentHp;
                if (us.Bars != null && _bars.ContainsKey(us.UnitId))
                    foreach (var kvp in us.Bars)
                        if (_bars[us.UnitId].ContainsKey(kvp.Key))
                            _bars[us.UnitId][kvp.Key] = kvp.Value;
            }
            // Restore thermal active effects (slow, burning) from bar values after state is applied.
            // Frozen state is not persisted in snapshots, so it is intentionally not restored.
            foreach (var u in _allUnits)
                if (_hp[u.Id] > 0) SyncThermalActiveEffects(u.Id);
            foreach (var u in _allUnits)
                if (_hp[u.Id] > 0) SyncBleedActiveEffects(u.Id);
            _turnIndex = NextAliveIndexAfter(r.LastActorId);

            CheckEnd();
            if (_isOver)
            {
                AddEvent("system", "The battle was already over at this point.", "takeover");
                return BuildResponse(Array.Empty<BattleEvent>());
            }

            AddEvent("system", $"You took control at step {r.AtStep}.", "takeover");
            return BuildResponse(AutoAdvance());
        }

        private BattleResponse HandlePlayerAction(PlayerActionRequest r)
        {
            if (!_started) throw new InvalidOperationException("Session not started.");
            if (_isOver) throw new InvalidOperationException("Battle is over.");
            if (_turnOrder[_turnIndex].Team != "player")
                throw new InvalidOperationException("Not a player turn.");

            var actor = _turnOrder[_turnIndex];
            var newEvents = new List<BattleEvent>();

            // Start-of-turn phase — only once per actual turn (Focus refunds the action, not the full turn).
            if (!_startOfTurnDone)
            {
                newEvents.AddRange(StartOfTurn(actor));
                _startOfTurnDone = true;
                if (_isOver) return BuildResponse(newEvents);
            }

            // If the player is frozen, their chosen action is skipped and the turn is consumed.
            if (HasActiveEffect(actor.Id, ThermalSystem.StatusFrozen))
            {
                RemoveActiveEffect(actor.Id, ThermalSystem.StatusFrozen);
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} is frozen and cannot act!", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                newEvents.AddRange(AutoAdvance());
                return BuildResponse(newEvents);
            }

            // If the player is stunned, their chosen action is skipped and the turn is consumed.
            if (_stunnedUnits.Contains(actor.Id))
            {
                _stunnedUnits.Remove(actor.Id);
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} is stunned and cannot act!", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                newEvents.AddRange(AutoAdvance());
                return BuildResponse(newEvents);
            }

            var skill = actor.ResolvedSkills.FirstOrDefault(s => s.Id == r.SkillId)
                         ?? throw new ArgumentException($"Unknown skill: {r.SkillId}");

            List<BattleUnit> targets;
            if (skill.Range == SkillRange.Self)
            {
                // Self-range skills always target the actor; no target ID needed.
                targets = new List<BattleUnit> { actor };
            }
            else if (skill.IsAoe)
            {
                targets = _allUnits.Where(u => TargetSideMatch(u, actor, skill.Target) && _hp[u.Id] > 0).ToList();
            }
            else
            {
                var t = _allUnits.FirstOrDefault(u => u.Id == r.TargetId)
                        ?? throw new ArgumentException($"Unknown target: {r.TargetId}");
                targets = new List<BattleUnit> { t };
            }

            newEvents.AddRange(ExecuteAction(actor, targets, skill));

            // If the action was refunded (e.g., Focus skill), return without advancing the turn.
            // The actor remains active and the player must submit their next action.
            if (_actionRefunded)
            {
                _actionRefunded = false;
                return BuildResponse(newEvents);
            }

            if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
            newEvents.AddRange(AutoAdvance());
            return BuildResponse(newEvents);
        }

        private BattleResponse HandleAutoPlayerAction(AutoPlayerActionRequest _)
        {
            if (!_started) throw new InvalidOperationException("Session not started.");
            if (_isOver) throw new InvalidOperationException("Battle is over.");
            if (_turnOrder[_turnIndex].Team != "player")
                throw new InvalidOperationException("Not a player turn.");

            var actor = _turnOrder[_turnIndex];
            var newEvents = new List<BattleEvent>();

            if (!_startOfTurnDone)
            {
                newEvents.AddRange(StartOfTurn(actor));
                _startOfTurnDone = true;
                if (_isOver) return BuildResponse(newEvents);
            }

            if (HasActiveEffect(actor.Id, ThermalSystem.StatusFrozen))
            {
                RemoveActiveEffect(actor.Id, ThermalSystem.StatusFrozen);
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} is frozen and cannot act!", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                newEvents.AddRange(AutoAdvance());
                return BuildResponse(newEvents);
            }

            if (_stunnedUnits.Contains(actor.Id))
            {
                _stunnedUnits.Remove(actor.Id);
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} is stunned and cannot act!", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                newEvents.AddRange(AutoAdvance());
                return BuildResponse(newEvents);
            }

            var skill = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive && GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor) && (s.FocusCost == 0 || GetBar(actor.Id, "focus") >= s.FocusCost)).LastOrDefault();
            if (skill == null)
            {
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} waits.", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                newEvents.AddRange(AutoAdvance());
                return BuildResponse(newEvents);
            }

            var targets = ResolveAutoTargets(actor, skill);
            if (targets.Count == 0) { CheckEnd(); return BuildResponse(Array.Empty<BattleEvent>()); }

            newEvents.AddRange(ExecuteAction(actor, targets, skill));
            if (_actionRefunded) _actionRefunded = false; // auto-mode: treat refund as consumed, advance normally
            if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
            newEvents.AddRange(AutoAdvance());
            return BuildResponse(newEvents);
        }

        private BattleResponse HandleAdvanceOneTurn(AdvanceOneTurnRequest _)
        {
            if (!_started) throw new InvalidOperationException("Session not started.");
            if (_isOver) return BuildResponse(Array.Empty<BattleEvent>());

            var actor = _turnOrder[_turnIndex];
            var newEvents = new List<BattleEvent>();

            if (!_startOfTurnDone)
            {
                newEvents.AddRange(StartOfTurn(actor));
                _startOfTurnDone = true;
                if (_isOver) return BuildResponse(newEvents);
            }

            if (HasActiveEffect(actor.Id, ThermalSystem.StatusFrozen))
            {
                // Consume the frozen turn: skip the action and advance.
                RemoveActiveEffect(actor.Id, ThermalSystem.StatusFrozen);
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} is frozen and cannot act!", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                return BuildResponse(newEvents);
            }

            if (_stunnedUnits.Contains(actor.Id))
            {
                // Consume the stunned turn: skip the action and advance.
                _stunnedUnits.Remove(actor.Id);
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} is stunned and cannot act!", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                return BuildResponse(newEvents);
            }

            var skill = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive && GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor) && (s.FocusCost == 0 || GetBar(actor.Id, "focus") >= s.FocusCost)).LastOrDefault();
            if (skill == null)
            {
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} waits.", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                return BuildResponse(newEvents);
            }

            var targets = ResolveAutoTargets(actor, skill);
            if (targets.Count == 0) { CheckEnd(); return BuildResponse(newEvents); }

            newEvents.AddRange(ExecuteAction(actor, targets, skill));
            if (_actionRefunded) _actionRefunded = false; // step mode: treat refund as consumed, advance normally
            if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
            return BuildResponse(newEvents);
        }

        // ── Response builder ─────────────────────────────────────────────────

        private BattleResponse BuildResponse(IReadOnlyList<BattleEvent> newEvents)
        {
            BattlePendingInput? pending = null;
            if (!_isOver && _turnOrder[_turnIndex].Team == "player")
            {
                var actor = _turnOrder[_turnIndex];
                var skills = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive).ToArray();
                pending = new BattlePendingInput(
                    Actor: actor,
                    Skills: skills,
                    AvailableSkillIds: skills.Where(s => GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor) && (s.FocusCost == 0 || GetBar(actor.Id, "focus") >= s.FocusCost)).Select(s => s.Id).ToArray(),
                    EnemyTargets: _allUnits.Where(u => u.Team != actor.Team && _hp[u.Id] > 0).ToArray(),
                    AllyTargets: _allUnits.Where(u => u.Team == actor.Team && _hp[u.Id] > 0).ToArray(),
                    SkillCooldowns: skills.Where(s => GetCooldown(s.Id) > 0)
                                             .ToDictionary(s => s.Id, s => _skillCooldowns[s.Id])
                );
            }

            return new BattleResponse(
                NewEvents: newEvents,
                FullLog: _log,
                State: _allUnits.Select(u => new UnitState(u.Id, _hp[u.Id], _hp[u.Id] > 0,
                    _bars[u.Id].Count > 0 ? new Dictionary<string, int>(_bars[u.Id]) : null,
                    BuildStatusEffects(u.Id),
                    BuildActiveEffectViews(u.Id))).ToArray(),
                PendingInput: pending,
                IsOver: _isOver,
                WinningTeam: _winningTeam,
                Round: _round
            );
        }

        // ── Internals ────────────────────────────────────────────────────────

        /// <summary>Auto-resolves enemy turns until it's a non-frozen, non-stunned player's turn or the battle ends.</summary>
        private IReadOnlyList<BattleEvent> AutoAdvance()
        {
            var produced = new List<BattleEvent>();
            while (!_isOver)
            {
                var actor = _turnOrder[_turnIndex];
                bool isFrozen = HasActiveEffect(actor.Id, ThermalSystem.StatusFrozen);
                bool isStunned = _stunnedUnits.Contains(actor.Id);

                // Stop when it is a living, non-frozen, non-stunned player's turn — they need to provide input.
                if (!isFrozen && !isStunned && actor.Team == "player") break;

                // Process start-of-turn effects for this actor — only once per actual turn.
                // When Focus refunds the action, the loop continues for the same actor without re-running these.
                if (!_startOfTurnDone)
                {
                    produced.AddRange(StartOfTurn(actor));
                    _startOfTurnDone = true;
                    if (_isOver) break;
                }

                if (isFrozen)
                {
                    // Consume the frozen turn: skip the action and advance.
                    RemoveActiveEffect(actor.Id, ThermalSystem.StatusFrozen);
                    produced.Add(AddEvent(actor.Id, $"{actor.Name} is frozen and cannot act!", "status"));
                    if (AdvanceTurn()) produced.AddRange(StartOfRound());
                    continue;
                }

                if (isStunned)
                {
                    // Consume the stunned turn: skip the action and advance.
                    _stunnedUnits.Remove(actor.Id);
                    produced.Add(AddEvent(actor.Id, $"{actor.Name} is stunned and cannot act!", "status"));
                    if (AdvanceTurn()) produced.AddRange(StartOfRound());
                    continue;
                }

                // Auto-resolve enemy action.
                var skill = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive && GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor) && (s.FocusCost == 0 || GetBar(actor.Id, "focus") >= s.FocusCost)).LastOrDefault();
                if (skill == null)
                {
                    produced.Add(AddEvent(actor.Id, $"{actor.Name} waits.", "status"));
                    if (AdvanceTurn()) produced.AddRange(StartOfRound());
                    continue;
                }

                var targets = ResolveAutoTargets(actor, skill);
                if (targets.Count == 0) { CheckEnd(); break; }

                produced.AddRange(ExecuteAction(actor, targets, skill));

                // If the skill refunded the action (e.g., AI used the Focus skill),
                // continue the loop for the same actor — StartOfTurn guard prevents re-running.
                if (_actionRefunded)
                {
                    _actionRefunded = false;
                    continue;
                }

                if (AdvanceTurn()) produced.AddRange(StartOfRound());
            }
            return produced;
        }

        /// <summary>Resolves the target list for auto actions (AI and auto mode).</summary>
        private List<BattleUnit> ResolveAutoTargets(BattleUnit actor, BattleSkill skill)
        {
            // Self-range skills always target the actor.
            if (skill.Range == SkillRange.Self)
                return new List<BattleUnit> { actor };

            if (skill.Target == BattleSkillTarget.Ally)
            {
                var allies = _allUnits.Where(u => u.Team == actor.Team && _hp[u.Id] > 0).ToList();
                if (allies.Count == 0) return new List<BattleUnit>();
                // Heal the lowest HP ally
                var target = skill.IsAoe ? null : allies.OrderBy(u => _hp[u.Id]).FirstOrDefault();
                return skill.IsAoe ? allies : new List<BattleUnit> { target! };
            }
            else
            {
                var foes = _allUnits.Where(u => u.Team != actor.Team && _hp[u.Id] > 0).ToList();
                if (foes.Count == 0) return new List<BattleUnit>();
                return skill.IsAoe ? foes : new List<BattleUnit> { foes[_rng.Next(foes.Count)] };
            }
        }

        private static bool TargetSideMatch(BattleUnit u, BattleUnit actor, BattleSkillTarget side) =>
            side == BattleSkillTarget.Ally
                ? u.Team == actor.Team
                : u.Team != actor.Team;

        /// <summary>
        /// Returns the remaining cooldown for a skill, or 0 if not on cooldown.
        /// Replaces Dictionary.GetValueOrDefault() which is not available in netstandard2.1.
        /// </summary>
        private int GetCooldown(string skillId) =>
            _skillCooldowns.TryGetValue(skillId, out int cd) ? cd : 0;

        private int GetBar(string unitId, string key) =>
            _bars[unitId].TryGetValue(key, out int v) ? v : 0;

        private IReadOnlyList<BattleEvent> ExecuteAction(BattleUnit actor, List<BattleUnit> targets, BattleSkill skill)
        {
            var produced = new List<BattleEvent>();

            // Tick down this actor's cooldowns (one turn has passed for them)
            foreach (var s in actor.ResolvedSkills)
                if (_skillCooldowns.TryGetValue(s.Id, out int cd) && cd > 0)
                    _skillCooldowns[s.Id] = cd - 1;
            // Also tick the actor's reaction skill cooldown.
            if (actor.ReactionSkill != null && _skillCooldowns.TryGetValue(actor.ReactionSkill.Id, out int rcd) && rcd > 0)
                _skillCooldowns[actor.ReactionSkill.Id] = rcd - 1;

            // Deduct focus cost for any skill that requires Focus.
            // Availability is already filtered in BuildResponse and BattleSession, but we guard
            // here too in case the engine is driven directly (e.g. tests, AI).
            if (skill.FocusCost > 0)
            {
                int currentFocus = GetBar(actor.Id, "focus");
                if (currentFocus < skill.FocusCost)
                {
                    produced.Add(AddEvent(actor.Id, $"{actor.Name} does not have enough Focus!", "status"));
                    return produced;
                }
                _bars[actor.Id]["focus"] = currentFocus - skill.FocusCost;
            }

            // Resolve the effective skill for this action: applies any runtime skill modifiers
            // from active effects on the actor. The base compiled skill is never mutated.
            var effectiveSkill = ResolveEffectiveSkill(actor, skill);

            // Consume the skill's cost from the unit's mana bar, if they have one.
            if (_bars[actor.Id].ContainsKey("mp"))
                _bars[actor.Id]["mp"] = Math.Max(0, _bars[actor.Id]["mp"] - effectiveSkill.Cost);

            // Determine event type from skill modifier
            string evType = (effectiveSkill.IsHeal || effectiveSkill.IsShield || effectiveSkill.IsRestoreBar) ? "skill"
                           : effectiveSkill.IsBasic ? "attack" : effectiveSkill.IsUltimate ? "soulburn" : "skill";

            // Focused buff: consumed by the first IsFocusCompatible skill used after a Focus action.
            // Grants the skill's configured effect (extra hit, extra projectile, etc.).
            // Does not modify damage multipliers — Focus is a precision layer, not a raw damage boost.
            bool isFocusEmpowered = _focusedUnits.Contains(actor.Id) && effectiveSkill.IsFocusCompatible;
            if (isFocusEmpowered)
            {
                _focusedUnits.Remove(actor.Id);
                produced.Add(AddEvent(actor.Id, $"{actor.Name}'s Focus sharpens {effectiveSkill.Name}!", "skill"));
            }
            // Fury scaling: STR-tagged skills deal more damage based on the actor's current Fury.
            // Scales continuously — no consumption. High Fury = stronger hits; low Fury = normal hits.
            double furyDamageMult = 1.0;
            if (actor.HasTrait(BattleTrait.Fury) && effectiveSkill.IsStrSkill && effectiveSkill.FuryDamageScale > 0)
            {
                int currentFury = GetBar(actor.Id, FurySystem.BarFury);
                furyDamageMult = FurySystem.ComputeDamageBonus(currentFury, effectiveSkill.FuryDamageScale);
            }
            // Flat (global) DamageDealtMultiplier from active effects stacks multiplicatively.
            double empowerMult = GetFlatDamageDealtMultiplier(actor.Id) * furyDamageMult;

            if (effectiveSkill.IsAoe && targets.Count > 1)
            {
                string aoeDesc = effectiveSkill.IsShield
                    ? $"{actor.Name} conjures {effectiveSkill.Name} on all allies!"
                    : $"{actor.Name} unleashes {effectiveSkill.Name} on all enemies!";
                produced.Add(AddEvent(actor.Id, aoeDesc, evType));
            }

            // Determine effective hit count and per-hit damage multiplier.
            // When skill.BaseHits != 1.0 or ScalingHits is set, the skill controls hit count:
            //   raw = clamp(BaseHits + Σ scalingHits, min 0.5)
            //   floor(raw) hits, each dealing DamageMultiplier × (raw / floor(raw)) × base.
            //   At raw = 0.5: 1 hit at 50% power. Total damage = DamageMultiplier × raw × base.
            // When BaseHits is 1.0 and ScalingHits is empty, the AGI-derived count applies,
            // subject to the slow debuff (slow halves the base-hit contribution).
            int effectiveHits;
            double perHitHitsMult;
            bool hasHitsOverride = effectiveSkill.BaseHits != 1.0 || (effectiveSkill.ScalingHits != null && effectiveSkill.ScalingHits.Count > 0);
            if (!effectiveSkill.IsHeal && !effectiveSkill.IsShield && !effectiveSkill.IsRestoreBar && hasHitsOverride)
            {
                double rawHits = effectiveSkill.BaseHits;
                if (effectiveSkill.ScalingHits != null)
                    foreach (var s in effectiveSkill.ScalingHits)
                        rawHits += actor.GetStat(s.Stat) * s.Scale;
                rawHits = Math.Max(0.5, rawHits);
                effectiveHits = Math.Max(1, (int)Math.Floor(rawHits));
                perHitHitsMult = rawHits / effectiveHits;
            }
            else
            {
                // AGI-derived hit count, reduced by slow if active.
                bool actorIsSlow = HasActiveEffect(actor.Id, ThermalSystem.StatusSlow);
                effectiveHits = (effectiveSkill.IsHeal || effectiveSkill.IsShield || effectiveSkill.IsRestoreBar) ? 1 : ThermalSystem.ResolveAgiHits(actor.Agi, actorIsSlow);
                perHitHitsMult = 1.0;
            }

            // Focus ExtraHit / ExtraProjectile: add the configured number of extra hits.
            // Extra hits share the same per-hit power as the base hits (perHitHitsMult unchanged).
            if (isFocusEmpowered && (effectiveSkill.FocusEffect == FocusEffectKind.ExtraHit || effectiveSkill.FocusEffect == FocusEffectKind.ExtraProjectile))
                effectiveHits += Math.Max(1, (int)effectiveSkill.FocusEffectValue);

            var effect = effectiveSkill.Effects.Count > 0 ? effectiveSkill.Effects[0] : null;

            // Dizzy: actor's final damage output is reduced when their disruption bar is at/above the dizzy threshold.
            // Stunned actors don't reach ExecuteAction (turn is skipped), so no need to exclude them here.
            bool actorIsDizzy = GetBar(actor.Id, DisruptionSystem.BarDisruption) >= DisruptionSystem.DizzyThreshold;

            foreach (var target in targets)
            {
                // ApplyEffect: applies an active buff/debuff once per target, not per hit.
                if (effect?.Kind == EffectKind.ApplyEffect && effect.EffectDefinition != null)
                {
                    ApplyActiveEffect(target.Id, effect.EffectDefinition, actor.Id);
                    string gainMsg = target.Id == actor.Id
                        ? $"{actor.Name} gains {effect.EffectDefinition.Name}!"
                        : $"{actor.Name} grants {effect.EffectDefinition.Name} to {target.Name}!";
                    produced.Add(AddEvent(actor.Id, gainMsg, evType, target.Id, skillId: skill.Id));
                    continue;
                }

                // GrantFocusedBuff: self-targeting setup effect. Adds actor to _focusedUnits.
                // Applied once per target (which is Self), not per hit. No damage is dealt.
                if (effect?.Kind == EffectKind.GrantFocusedBuff)
                {
                    _focusedUnits.Add(actor.Id);
                    produced.Add(AddEvent(actor.Id, $"{actor.Name} focuses intently!", "skill", skillId: skill.Id));
                    continue;
                }

                // Dispel: removes one random buff or debuff from the target.
                // Applied once per target, not per hit. No damage is dealt.
                if (effect?.Kind == EffectKind.Dispel)
                {
                    EffectAlignment? alignment = effect.DispelAlignment;
                    var candidates = CollectDispelCandidates(target.Id, alignment);
                    if (candidates.Count == 0)
                    {
                        string noneKind = alignment == EffectAlignment.Buff ? "buff" : "debuff";
                        produced.Add(AddEvent(actor.Id, $"{target.Name} has no {noneKind} to dispel!", "status", target.Id, skillId: skill.Id));
                    }
                    else
                    {
                        string chosen = candidates[_rng.Next(candidates.Count)];
                        string removedName = GetEffectDisplayName(target.Id, chosen);
                        DispelEffect(target.Id, chosen);
                        produced.Add(AddEvent(actor.Id, $"{actor.Name} dispels {removedName} from {target.Name}!", evType, target.Id, skillId: skill.Id));
                    }
                    continue;
                }

                for (int i = 0; i < effectiveHits; i++)
                {
                    if (effect?.Kind == EffectKind.Heal)
                    {
                        // Heal: sum scaling contributions from all components, apply damageMultiplier + empowerMult.
                        double healRaw = 0;
                        foreach (var comp in effect.DamagePerHit)
                            foreach (var s in comp.Scaling)
                                healRaw += actor.GetStat(s.Stat) * s.Scale;
                        healRaw *= effectiveSkill.DamageMultiplier * empowerMult;
                        int healVariance = Math.Max(1, (int)healRaw / 5);
                        int amount = Math.Max(0, (int)healRaw + _rng.Next(-healVariance, healVariance + 1));
                        amount = (int)(amount * GetReceivingHealingMultiplier(target.Id));
                        var maxHp = _allUnits.First(u => u.Id == target.Id).MaxHp;
                        int healed = Math.Min(amount, Math.Max(0, maxHp - _hp[target.Id]));
                        _hp[target.Id] += healed;
                        produced.Add(AddEvent(actor.Id,
                            $"{actor.Name} heals {target.Name} for {healed} HP.",
                            evType, target.Id, healed,
                            skillId: skill.Id));
                    }
                    else if (effect?.Kind == EffectKind.Shield)
                    {
                        // Shield: grant barrier using the same scaling formula as healing.
                        // Barrier absorbs damage before HP. Cannot be healed; decays each round based on WIS.
                        double shieldRaw = 0;
                        foreach (var comp in effect.DamagePerHit)
                            foreach (var s in comp.Scaling)
                                shieldRaw += actor.GetStat(s.Stat) * s.Scale;
                        shieldRaw *= effectiveSkill.DamageMultiplier * empowerMult;
                        int shieldVariance = Math.Max(1, (int)shieldRaw / 5);
                        int amount = Math.Max(0, (int)shieldRaw + _rng.Next(-shieldVariance, shieldVariance + 1));
                        amount = (int)(amount * GetReceivingBarrierMultiplier(target.Id));
                        if (!_bars[target.Id].ContainsKey("barrier"))
                            _bars[target.Id]["barrier"] = 0;
                        _bars[target.Id]["barrier"] += amount;
                        produced.Add(AddEvent(actor.Id,
                            effectiveSkill.IsAoe
                                ? $"  \u2192 {target.Name} gains {amount} barrier."
                                : $"{actor.Name} grants {amount} barrier to {target.Name}.",
                            evType, target.Id, amount,
                            skillId: skill.Id));
                    }
                    else if (effect?.Kind == EffectKind.RestoreBar && effect.BarKey != null)
                    {
                        // RestoreBar: add a fixed amount to a named secondary bar on the target.
                        // Positive BarAmount restores; negative drains.
                        string barKey = effect.BarKey;
                        if (_bars[target.Id].ContainsKey(barKey))
                        {
                            var targetUnit = _allUnits.First(u => u.Id == target.Id);
                            int current = GetBar(target.Id, barKey);
                            int max = targetUnit.MaxBars.TryGetValue(barKey, out int m) ? m : int.MaxValue;
                            int clamped = Math.Min(Math.Max(current + effect.BarAmount, 0), max);
                            _bars[target.Id][barKey] = clamped;
                            int actual = clamped - current;
                            string barName = barKey.ToUpperInvariant() switch
                            {
                                "MP" => "MP",
                                "FOCUS" => "Focus",
                                "FURY" => "Fury",
                                _ => char.ToUpperInvariant(barKey[0]) + barKey.Substring(1),
                            };
                            string desc = actual >= 0
                                ? (target.Id == actor.Id
                                    ? $"{actor.Name} restores {actual} {barName}."
                                    : $"{actor.Name} restores {actual} {barName} for {target.Name}.")
                                : (target.Id == actor.Id
                                    ? $"{actor.Name} loses {-actual} {barName}."
                                    : $"{actor.Name} drains {-actual} {barName} from {target.Name}.");
                            produced.Add(AddEvent(actor.Id, desc, evType, target.Id, actual, skillId: skill.Id));
                        }
                    }
                    else
                    {
                        var components = effect?.DamagePerHit ?? System.Array.Empty<DamageComponent>();
                        // Pass all per-hit modifiers into the pipeline so the full audit trail
                        // in DamageResult.Steps explains the final number without external adjustments.
                        double attackerOutputMult = actorIsDizzy ? DisruptionSystem.DizzyDamageMultiplier : 1.0;
                        double damageTakenMult = GetDamageTakenMultiplier(target.Id);
                        var hitResults = DamageCalc.Compute(actor, target, components, effectiveSkill.DamageMultiplier,
                            empowerMult * perHitHitsMult, _rng,
                            et => GetEffectiveResistance(target.Id, et),
                            et => GetEffectivePenetration(actor.Id, et),
                            et => GetOutgoingTypeMultiplier(actor.Id, et),
                            et => GetIncomingTypeMultiplier(target.Id, et),
                            attackerOutputMult,
                            damageTakenMult);
                        int totalDamage = 0;
                        foreach (var r in hitResults) totalDamage += r.FinalDamage;

                        var primaryType = effectiveSkill.PrimaryEffectType;

                        // Barrier absorbs damage before HP.
                        int barrierCurrent = GetBar(target.Id, "barrier");
                        int barrierAbsorbed = Math.Min(barrierCurrent, totalDamage);
                        if (barrierAbsorbed > 0)
                            _bars[target.Id]["barrier"] = barrierCurrent - barrierAbsorbed;
                        int hpBefore = _hp[target.Id];
                        _hp[target.Id] = Math.Max(0, hpBefore - (totalDamage - barrierAbsorbed));
                        int hpActuallyLost = hpBefore - _hp[target.Id];

                        string hitLabel = effectiveHits > 1 ? $" (hit {i + 1}/{effectiveHits})" : "";
                        produced.Add(AddEvent(actor.Id,
                            effectiveSkill.IsAoe
                                ? $"  \u2192 {target.Name} takes {totalDamage} damage{hitLabel}."
                                : $"{actor.Name} uses {effectiveSkill.Name} on {target.Name} for {totalDamage} damage{hitLabel}.",
                            evType, target.Id, totalDamage,
                            skillId: skill.Id, effectType: primaryType,
                            hitIndex: i, totalHits: effectiveHits));

                        // Fury: target gains fury from taking direct damage (flat + HP% bonus).
                        // Gain is per hit to reward tanking multiple strikes; HP% rewards low-armour builds.
                        if (target.HasTrait(BattleTrait.Fury) && hpActuallyLost > 0)
                        {
                            int furyGain = FurySystem.ComputeHitGain(hpActuallyLost, target.MaxHp);
                            _bars[target.Id][FurySystem.BarFury] = Math.Min(FurySystem.MaxBar,
                                GetBar(target.Id, FurySystem.BarFury) + furyGain);
                        }

                        // Thermal, disruption, and bleed CC buildup from the damage hit results.
                        produced.AddRange(ApplyCCBuildup(actor, target, hitResults));

                        // Disruption buildup: apply per-hit disruption power declared on the skill effect.
                        if (effect != null && effect.DisruptionPower > 0)
                            produced.AddRange(ApplyDisruptionBuildup(actor, target, effect.DisruptionPower));

                        if (_hp[target.Id] <= 0)
                        {
                            produced.Add(AddEvent(target.Id, $"{target.Name} is defeated!", "death"));
                            break;
                        }
                    }
                }
            }

            // Apply cooldown for the used skill (uses the base skill's cooldown, not the effective one,
            // so that runtime cooldown modifiers do not permanently alter the session's cooldown state).
            if (skill.Cooldown > 0)
                _skillCooldowns[skill.Id] = skill.Cooldown;

            // RefundsAction: the actor gets another action immediately without advancing turn order.
            // Skip post-action processing (fury gain, active-effect ticking, reactions) — those belong
            // to the follow-up action so they are not applied twice in the same logical turn.
            if (skill.RefundsAction)
            {
                _actionRefunded = true;
                CheckEnd();
                return produced;
            }

            // Fury: actor gains Fury once when using a STR-tagged skill.
            // Granted once per skill execution, not per hit or per target.
            if (actor.HasTrait(BattleTrait.Fury) && effectiveSkill.IsStrSkill)
                _bars[actor.Id][FurySystem.BarFury] = Math.Min(FurySystem.MaxBar,
                    GetBar(actor.Id, FurySystem.BarFury) + FurySystem.SkillUseGain);

            // Tick active effects: decrement durations and remove expired instances.
            produced.AddRange(TickActiveEffects(actor.Id));

            // Reactions: fire after the full action resolves, before end-of-action checks.
            // Reactions cannot trigger further reactions (no chaining).
            produced.AddRange(ExecuteReactions(actor, targets, effectiveSkill));

            CheckEnd();
            return produced;
        }

        // ── Reaction helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Checks every unit that was in <paramref name="hitTargets"/> for a qualifying reaction
        /// and fires it if all conditions are met. Reactions fire after the full action resolves
        /// and cannot themselves trigger further reactions.
        /// </summary>
        private IReadOnlyList<BattleEvent> ExecuteReactions(
            BattleUnit actor, List<BattleUnit> hitTargets, BattleSkill usedSkill)
        {
            var produced = new List<BattleEvent>();

            // Collect eligible reactors in AGI descending order (fastest reacts first).
            var reactors = hitTargets
                .Where(t => _hp[t.Id] > 0                          // must be alive after the action
                    && t.ReactionSkill != null
                    && !HasActiveEffect(t.Id, ThermalSystem.StatusFrozen) // CC suppresses reactions
                    && !_stunnedUnits.Contains(t.Id)
                    && GetCooldown(t.ReactionSkill!.Id) <= 0)       // not on cooldown
                .OrderByDescending(t => t.Agi)
                .ToList();

            foreach (var reactor in reactors)
            {
                var reaction = reactor.ReactionSkill!;
                bool triggers = reaction.Trigger switch
                {
                    ReactionTrigger.OnHitBy => MatchesOnHitBy(reaction, usedSkill),
                    _ => false,
                };
                if (!triggers) continue;
                if (_hp[actor.Id] <= 0) continue;   // attacker already dead

                produced.Add(AddEvent(reactor.Id,
                    $"{reactor.Name} counters with {reaction.Name}!", "reaction"));
                produced.AddRange(ExecuteReactionAction(reactor, actor, reaction));

                // Put the reaction skill on cooldown.
                if (reaction.Cooldown > 0)
                    _skillCooldowns[reaction.Id] = reaction.Cooldown;
            }

            return produced;
        }

        /// <summary>
        /// Returns true when <paramref name="usedSkill"/> satisfies all <see cref="TriggerCondition"/>
        /// entries on <paramref name="reaction"/> for the <see cref="ReactionTrigger.OnHitBy"/> trigger.
        /// The action must deal damage (not a heal, shield, or bar restore).
        /// When the skill's <see cref="BattleSkill.TriggerConditions"/> list is empty, any damaging
        /// action qualifies.
        /// </summary>
        private static bool MatchesOnHitBy(BattleSkill reaction, BattleSkill usedSkill)
        {
            // Only fire for damaging actions — heals and shields do not "hit" the target.
            if (usedSkill.IsHeal || usedSkill.IsShield || usedSkill.IsRestoreBar) return false;

            var conditions = reaction.TriggerConditions;
            if (conditions == null || conditions.Count == 0) return true;

            foreach (var condition in conditions)
            {
                if (condition.Range != null && usedSkill.Range != condition.Range) return false;

                if (condition.DamageType != null)
                {
                    bool hasType = false;
                    foreach (var effect in usedSkill.Effects)
                    {
                        foreach (var dc in effect.DamagePerHit)
                        {
                            if (dc.DamageType == condition.DamageType.Value)
                            {
                                hasType = true;
                                break;
                            }
                        }
                        if (hasType) break;
                    }
                    if (!hasType) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Executes a reaction skill fired by <paramref name="reactor"/> against
        /// <paramref name="target"/> (the original attacker). Simplified execution:
        /// no CD ticking, no MP cost, no Focus/Fury empowerment, no further reaction triggering.
        /// Always executes a single hit unless the skill specifies BaseHits.
        /// </summary>
        private IReadOnlyList<BattleEvent> ExecuteReactionAction(
            BattleUnit reactor, BattleUnit target, BattleSkill reaction)
        {
            var produced = new List<BattleEvent>();
            var effect = reaction.Effects.Count > 0 ? reaction.Effects[0] : null;
            if (effect == null || effect.Kind != EffectKind.Damage)
                return produced;

            // Hit count: use skill BaseHits if specified; otherwise 1 (no AGI scaling for reactions).
            int hits = reaction.BaseHits != 1.0
                ? Math.Max(1, (int)Math.Floor(reaction.BaseHits))
                : 1;
            double perHitMult = reaction.BaseHits != 1.0 ? reaction.BaseHits / hits : 1.0;

            for (int i = 0; i < hits; i++)
            {
                var hitResults = DamageCalc.Compute(reactor, target, effect.DamagePerHit,
                    reaction.DamageMultiplier,
                    perHitMult, _rng,
                    et => GetEffectiveResistance(target.Id, et),
                    et => GetEffectivePenetration(reactor.Id, et),
                    et => GetOutgoingTypeMultiplier(reactor.Id, et),
                    et => GetIncomingTypeMultiplier(target.Id, et));

                int totalDamage = 0;
                foreach (var r in hitResults) totalDamage += r.FinalDamage;

                // Barrier absorbs damage before HP.
                int barrierCurrent = GetBar(target.Id, "barrier");
                int barrierAbsorbed = Math.Min(barrierCurrent, totalDamage);
                if (barrierAbsorbed > 0)
                    _bars[target.Id]["barrier"] = barrierCurrent - barrierAbsorbed;
                _hp[target.Id] = Math.Max(0, _hp[target.Id] - (totalDamage - barrierAbsorbed));

                string hitLabel = hits > 1 ? $" (hit {i + 1}/{hits})" : "";
                produced.Add(AddEvent(reactor.Id,
                    $"  → {target.Name} takes {totalDamage} damage{hitLabel}.",
                    "reaction", target.Id, totalDamage,
                    skillId: reaction.Id, effectType: reaction.PrimaryEffectType,
                    hitIndex: i, totalHits: hits));

                if (_hp[target.Id] <= 0)
                {
                    produced.Add(AddEvent(target.Id, $"{target.Name} is defeated!", "death"));
                    break;
                }
            }

            return produced;
        }

        private bool AdvanceTurn()
        {
            if (_isOver) return false;
            // Clear the Focused buff and turn-start flag for the actor whose turn just ended.
            _focusedUnits.Remove(_turnOrder[_turnIndex].Id);
            _startOfTurnDone = false;
            int prev = _turnIndex;
            for (int i = 0; i < _turnOrder.Count; i++)
            {
                _turnIndex = (_turnIndex + 1) % _turnOrder.Count;
                if (_hp[_turnOrder[_turnIndex].Id] > 0)
                    return _turnIndex <= prev;  // wrapped = new round started
            }
            CheckEnd();
            return false;
        }

        // ── Thermal helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Processes the start-of-turn phase for <paramref name="actor"/>:
        /// applies burn DOT and decays thermal bars.
        /// Frozen-turn consumption is handled by callers.
        /// </summary>
        private IReadOnlyList<BattleEvent> StartOfTurn(BattleUnit actor)
        {
            var events = new List<BattleEvent>();
            if (_hp[actor.Id] <= 0) return events;

            int burnBar = GetBar(actor.Id, ThermalSystem.BarBurn);

            // Burn DOT: applied before decay so the damage reflects the current bar.
            if (burnBar >= ThermalSystem.BurningThreshold)
            {
                int dot = ThermalSystem.ComputeBurnDot(burnBar);
                if (dot > 0)
                {
                    int barrierCurrent = GetBar(actor.Id, "barrier");
                    int barrierAbsorbed = Math.Min(barrierCurrent, dot);
                    if (barrierAbsorbed > 0)
                        _bars[actor.Id]["barrier"] = barrierCurrent - barrierAbsorbed;
                    _hp[actor.Id] = Math.Max(0, _hp[actor.Id] - (dot - barrierAbsorbed));
                    events.Add(AddEvent(actor.Id, $"{actor.Name} takes {dot} burn damage!", "status",
                        targetId: actor.Id, value: dot));
                    CheckEnd();
                }
            }

            // Thermal bar decay.
            int coldBar = GetBar(actor.Id, ThermalSystem.BarCold);
            var (newCold, newBurn) = ThermalSystem.ApplyDecay(coldBar, burnBar);
            if (newCold != coldBar) _bars[actor.Id][ThermalSystem.BarCold] = newCold;
            if (newBurn != burnBar) _bars[actor.Id][ThermalSystem.BarBurn] = newBurn;

            // Sync thermal active effects after decay — bars may have dropped below thresholds.
            SyncThermalActiveEffects(actor.Id);

            // Disruption bar decay.
            int disruptionBar = GetBar(actor.Id, DisruptionSystem.BarDisruption);
            int newDisruption = DisruptionSystem.ApplyDecay(disruptionBar);
            if (newDisruption != disruptionBar) _bars[actor.Id][DisruptionSystem.BarDisruption] = newDisruption;

            // Bleed DOT: applied before decay so the damage reflects the current bar.
            int bleedBar = GetBar(actor.Id, BleedSystem.BarBleed);
            if (bleedBar >= BleedSystem.BleedingThreshold)
            {
                int bleedDot = BleedSystem.ComputeBleedDot(bleedBar);
                if (bleedDot > 0)
                {
                    int barrierCurrent = GetBar(actor.Id, "barrier");
                    int barrierAbsorbed = Math.Min(barrierCurrent, bleedDot);
                    if (barrierAbsorbed > 0)
                        _bars[actor.Id]["barrier"] = barrierCurrent - barrierAbsorbed;
                    _hp[actor.Id] = Math.Max(0, _hp[actor.Id] - (bleedDot - barrierAbsorbed));
                    events.Add(AddEvent(actor.Id, $"{actor.Name} takes {bleedDot} bleed damage!", "status",
                        targetId: actor.Id, value: bleedDot));
                    CheckEnd();
                }
            }

            // Bleed bar decay.
            int newBleed = BleedSystem.ApplyDecay(bleedBar);
            if (newBleed != bleedBar) _bars[actor.Id][BleedSystem.BarBleed] = newBleed;
            SyncBleedActiveEffects(actor.Id);

            // Focus regeneration: each turn, units with a Focus bar regain FocusRegenPerTurn points.
            if (_bars[actor.Id].TryGetValue("focus", out int currentFocus))
            {
                int maxFocus = actor.MaxBars.TryGetValue("focus", out int mf) ? mf : 100;
                int newFocus = Math.Min(currentFocus + FocusRegenPerTurn, maxFocus);
                if (newFocus != currentFocus) _bars[actor.Id]["focus"] = newFocus;
            }

            // Fury decay: at the end of each turn Fury falls by a flat amount.
            // Unconditional — applies regardless of what action was taken.
            if (_bars[actor.Id].TryGetValue(FurySystem.BarFury, out int currentFury))
            {
                int newFury = FurySystem.ApplyDecay(currentFury);
                if (newFury != currentFury) _bars[actor.Id][FurySystem.BarFury] = newFury;
            }

            return events;
        }

        /// <summary>
        /// Routes CC buildup from all damage results to the appropriate system:
        /// Fire/Cold → thermal bars, Blunt → disruption bar, Slash → bleed bar.
        /// Generates status events when CC thresholds are crossed.
        /// </summary>
        private IReadOnlyList<BattleEvent> ApplyCCBuildup(BattleUnit actor, BattleUnit target, IReadOnlyList<DamageResult> hitResults)
        {
            var events = new List<BattleEvent>();
            foreach (var r in hitResults)
            {
                if (r.BuildupPower <= 0) continue;
                if (r.EffectType == EffectType.Cold)
                {
                    int currentCold = GetBar(target.Id, ThermalSystem.BarCold);
                    int currentBurn = GetBar(target.Id, ThermalSystem.BarBurn);
                    int effectiveColdResistance = GetEffectiveResistance(target.Id, EffectType.Cold);
                    int thermalProtection = GetEffectiveThermalProtection(target.Id);
                    int boostedColdResistance = (int)(effectiveColdResistance * (1.0 + thermalProtection / 100.0));
                    ThermalSystem.ApplyCold(r.BuildupPower, boostedColdResistance,
                        currentBurn, currentCold, out int newBurn, out int newCold);
                    _bars[target.Id][ThermalSystem.BarBurn] = newBurn;
                    _bars[target.Id][ThermalSystem.BarCold] = newCold;

                    if (ThermalSystem.CheckFreezeTriggered(ref newCold))
                    {
                        _bars[target.Id][ThermalSystem.BarCold] = newCold;
                        // Apply the frozen active effect (replaces _frozenUnits.Add).
                        ApplyActiveEffect(target.Id, _frozenDef, target.Id);
                        // Frozen takes priority: remove slow if it was active.
                        RemoveActiveEffect(target.Id, ThermalSystem.StatusSlow);
                        events.Add(AddEvent(target.Id, $"{target.Name} is frozen!", "status"));
                    }
                }
                else if (r.EffectType == EffectType.Fire)
                {
                    int currentCold = GetBar(target.Id, ThermalSystem.BarCold);
                    int currentBurn = GetBar(target.Id, ThermalSystem.BarBurn);
                    int effectiveFireResistance = GetEffectiveResistance(target.Id, EffectType.Fire);
                    int thermalProtection = GetEffectiveThermalProtection(target.Id);
                    int boostedFireResistance = (int)(effectiveFireResistance * (1.0 + thermalProtection / 100.0));
                    ThermalSystem.ApplyFire(r.BuildupPower, boostedFireResistance,
                        currentCold, currentBurn, out int newCold, out int newBurn);
                    _bars[target.Id][ThermalSystem.BarCold] = newCold;
                    _bars[target.Id][ThermalSystem.BarBurn] = newBurn;
                }
                else if (r.EffectType == EffectType.Blunt)
                {
                    // Blunt damage builds disruption bar using DisruptionResistance/Penetration.
                    events.AddRange(ApplyDisruptionBuildup(actor, target, r.BuildupPower));
                }
                else if (r.EffectType == EffectType.Slash)
                {
                    // Slash damage builds bleed bar using the target's effective Slash resistance.
                    events.AddRange(ApplyBleedBuildup(target, r.BuildupPower));
                }
            }
            // Sync thermal active effects for the target after all hits resolve.
            SyncThermalActiveEffects(target.Id);
            return events;
        }

        // ── Disruption helpers ───────────────────────────────────────────────

        /// <summary>
        /// Applies disruption power to <paramref name="target"/>'s disruption bar for one hit.
        /// Generates stun events when the bar reaches the stun threshold.
        /// Uses runtime disruption-resistance and disruption-penetration modifiers from active effects.
        /// </summary>
        private IReadOnlyList<BattleEvent> ApplyDisruptionBuildup(BattleUnit actor, BattleUnit target, int disruptionPower)
        {
            var events = new List<BattleEvent>();
            int current = GetBar(target.Id, DisruptionSystem.BarDisruption);
            int effectiveDisruptionResistance = GetEffectiveDisruptionResistance(target.Id)
                - GetEffectiveDisruptionPenetration(actor.Id);
            int built = DisruptionSystem.ApplyDisruption(disruptionPower, effectiveDisruptionResistance, current, out int newBar);
            _bars[target.Id][DisruptionSystem.BarDisruption] = newBar;

            if (built > 0)
                events.Add(AddEvent(target.Id, $"{target.Name} takes {built} disruption.", "status"));

            if (DisruptionSystem.CheckStunTriggered(ref newBar))
            {
                _bars[target.Id][DisruptionSystem.BarDisruption] = newBar;
                _stunnedUnits.Add(target.Id);
                events.Add(AddEvent(target.Id, $"{target.Name} is stunned!", "status"));
            }

            return events;
        }

        // ── Bleed helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Applies bleed power to <paramref name="target"/>'s bleed bar for one hit.
        /// Bleed resistance is derived from the target's effective Slash resistance (stacks with Physical).
        /// Generates the <c>bleeding</c> active effect when the bar reaches <see cref="BleedSystem.BleedingThreshold"/>.
        /// </summary>
        private IReadOnlyList<BattleEvent> ApplyBleedBuildup(BattleUnit target, int bleedPower)
        {
            var events = new List<BattleEvent>();
            int current = GetBar(target.Id, BleedSystem.BarBleed);
            // Bleed resistance is the effective Slash resistance (which stacks with Physical resistance).
            int slashResistance = GetEffectiveResistance(target.Id, EffectType.Slash);
            int built = BleedSystem.ApplyBleed(bleedPower, slashResistance, current, out int newBar);
            _bars[target.Id][BleedSystem.BarBleed] = newBar;

            if (built > 0)
                events.Add(AddEvent(target.Id, $"{target.Name} takes {built} bleed buildup.", "status"));

            SyncBleedActiveEffects(target.Id);
            return events;
        }

        /// <summary>
        /// Applies or removes the <c>bleeding</c> active effect based on the unit's current bleed bar.
        /// Called after any bleed bar change (buildup or decay).
        /// </summary>
        private void SyncBleedActiveEffects(string unitId)
        {
            int bleedBar = GetBar(unitId, BleedSystem.BarBleed);

            // Scratched: active when bleed is building but below the bleeding threshold.
            bool shouldBeScratched = bleedBar > 0 && bleedBar < BleedSystem.BleedingThreshold;
            if (shouldBeScratched && !HasActiveEffect(unitId, BleedSystem.StatusScratched))
                ApplyActiveEffect(unitId, _scratchedDef, unitId);
            else if (!shouldBeScratched)
                RemoveActiveEffect(unitId, BleedSystem.StatusScratched);

            bool shouldBeBleeding = bleedBar >= BleedSystem.BleedingThreshold;
            if (shouldBeBleeding && !HasActiveEffect(unitId, BleedSystem.StatusBleeding))
                ApplyActiveEffect(unitId, _bleedingDef, unitId);
            else if (!shouldBeBleeding)
                RemoveActiveEffect(unitId, BleedSystem.StatusBleeding);
        }
        /// Thermal effects (slow/frozen/burning) are tracked as active effects and appear here automatically.
        /// Disruption effects (dizzy/stunned) are derived from bars and the stunned set.
        /// </summary>
        private IReadOnlyList<string>? BuildStatusEffects(string unitId)
        {
            if (_hp[unitId] <= 0) return null;

            int disruptionBar = GetBar(unitId, DisruptionSystem.BarDisruption);
            bool isStunned = _stunnedUnits.Contains(unitId);
            var disruptionEffects = DisruptionSystem.GetDisruptionStatusEffects(disruptionBar, isStunned);

            // Include the definition ID of each active runtime effect so the client can display icons.
            // Thermal status effects (slow, frozen, burning) are tracked as active effects.
            bool hasActiveEffects = _activeEffects.TryGetValue(unitId, out var effects) && effects.Count > 0;

            if (disruptionEffects.Count == 0 && !hasActiveEffects) return null;

            var all = new List<string>(disruptionEffects);
            if (hasActiveEffects)
                foreach (var e in effects!)
                    all.Add(e.DefinitionId);
            return all;
        }

        /// <summary>Called when the turn order wraps. Increments round, regenerates mana, decays barrier.</summary>
        private IReadOnlyList<BattleEvent> StartOfRound()
        {
            _round++;
            var events = new List<BattleEvent>();
            events.Add(AddEvent("system", $"\u2500\u2500 Round {_round} \u2500\u2500", "round"));
            // Barrier decay: each living unit loses a portion of their barrier based on WIS.
            // decay = max(5, 20 - WIS/10) % of remaining barrier, minimum 1.
            // Higher WIS → slower decay (WIS=60 → 14%, WIS=100 → 10%, WIS=0 → 20%).
            foreach (var u in _allUnits.Where(u => _hp[u.Id] > 0))
            {
                int barrier = GetBar(u.Id, "barrier");
                if (barrier <= 0) continue;
                int decayPct = Math.Max(5, 20 - u.Wis / 10);
                int decay = Math.Max(1, barrier * decayPct / 100);
                _bars[u.Id]["barrier"] = Math.Max(0, barrier - decay);
                events.Add(AddEvent(u.Id, $"{u.Name}'s barrier fades by {decay}.", "round"));
            }
            foreach (var u in _allUnits.Where(u => _bars[u.Id].ContainsKey("mp") && _hp[u.Id] > 0))
            {
                int maxMp = u.MaxBars["mp"];
                int regen = Math.Max(1, maxMp / 5);  // 20 % of MaxMp
                int gained = Math.Min(regen, maxMp - _bars[u.Id]["mp"]);
                if (gained > 0)
                {
                    _bars[u.Id]["mp"] += gained;
                    events.Add(AddEvent(u.Id, $"{u.Name} recovers {gained} MP.", "round"));
                }
            }
            return events;
        }

        private void CheckEnd()
        {
            if (_isOver) return;
            bool playerAlive = _allUnits.Any(u => u.Team == "player" && _hp[u.Id] > 0);
            bool enemyAlive = _allUnits.Any(u => u.Team == "enemy" && _hp[u.Id] > 0);
            if (!playerAlive || !enemyAlive)
            {
                _isOver = true;
                _winningTeam = playerAlive ? "player" : "enemy";
                AddEvent("system", playerAlive ? "Victory!" : "Defeat...", "end");
            }
        }

        private int NextAliveIndexAfter(string? actorId)
        {
            int lastIdx = actorId != null ? _turnOrder.FindIndex(u => u.Id == actorId) : -1;
            int start = lastIdx >= 0 ? (lastIdx + 1) % _turnOrder.Count : 0;
            for (int i = 0; i < _turnOrder.Count; i++)
            {
                int idx = (start + i) % _turnOrder.Count;
                if (_hp[_turnOrder[idx].Id] > 0) return idx;
            }
            return 0;
        }

        private BattleEvent AddEvent(string actorId, string description, string type,
            string? targetId = null, int value = 0,
            string? skillId = null, EffectType? effectType = null, int hitIndex = 0, int totalHits = 1)
        {
            var ev = new BattleEvent(actorId, description, type, targetId, value, skillId, effectType, hitIndex, totalHits);
            _log.Add(ev);
            return ev;
        }

        // ── Active-effect system ─────────────────────────────────────────────

        /// <summary>
        /// Applies a runtime active effect to a target unit.
        /// Stacking policy controls behaviour when the same definition is already active.
        /// </summary>
        internal void ApplyActiveEffect(string targetUnitId, ActiveEffectDefinition definition, string sourceUnitId)
        {
            if (!_activeEffects.ContainsKey(targetUnitId))
                _activeEffects[targetUnitId] = new List<ActiveEffectInstance>();

            var list = _activeEffects[targetUnitId];
            var existing = FindExistingInstance(list, definition.Id);

            switch (definition.StackingPolicy)
            {
                case EffectStackingPolicy.RefreshDuration:
                    if (existing != null)
                    {
                        int idx = list.IndexOf(existing);
                        list[idx] = existing with { RemainingDuration = definition.Duration };
                        return;
                    }
                    break;

                case EffectStackingPolicy.ReplaceIfStronger:
                    if (existing != null)
                    {
                        // Keep the existing instance if it has more duration remaining.
                        if (definition.Duration <= existing.RemainingDuration) return;
                        list.Remove(existing);
                    }
                    break;

                case EffectStackingPolicy.StackIntensity:
                    if (existing != null)
                    {
                        int idx = list.IndexOf(existing);
                        list[idx] = existing with
                        {
                            Stacks = existing.Stacks + 1,
                            RemainingDuration = Math.Max(existing.RemainingDuration, definition.Duration),
                        };
                        return;
                    }
                    break;

                case EffectStackingPolicy.IndependentInstances:
                    // Always fall through to create a new instance.
                    break;
            }

            string instanceId = $"effect_{_nextEffectId++}";
            list.Add(new ActiveEffectInstance(
                InstanceId: instanceId,
                DefinitionId: definition.Id,
                Name: definition.Name,
                SourceUnitId: sourceUnitId,
                TargetUnitId: targetUnitId,
                RemainingDuration: definition.Duration,
                DurationKind: definition.DurationKind,
                SkillModifier: definition.SkillModifier,
                StatModifiers: definition.StatModifiers,
                DamageDealtMultiplierByType: definition.DamageDealtMultiplierByType,
                DamageTakenMultiplierByType: definition.DamageTakenMultiplierByType,
                ResistanceModifierByType: definition.ResistanceModifierByType,
                PenetrationModifierByType: definition.PenetrationModifierByType,
                Alignment: definition.Alignment
            ));
        }

        /// <summary>Returns all active effect instances currently on a unit (read-only view).</summary>
        internal IReadOnlyList<ActiveEffectInstance> GetActiveEffects(string unitId) =>
            _activeEffects.TryGetValue(unitId, out var list) ? list : System.Array.Empty<ActiveEffectInstance>();

        /// <summary>
        /// Rebuilds and returns the current BattleResponse from the present runtime state.
        /// Used to refresh the external view after out-of-band state changes such as
        /// <see cref="ApplyActiveEffect"/> that do not go through a HandleRequest call.
        /// </summary>
        internal BattleResponse RebuildCurrentResponse() =>
            BuildResponse(System.Array.Empty<BattleEvent>());

        /// <summary>Resolves the effective skill for an action by overlaying runtime skill modifiers.</summary>
        private BattleSkill ResolveEffectiveSkill(BattleUnit actor, BattleSkill skill)
        {
            if (!_activeEffects.TryGetValue(actor.Id, out var effects) || effects.Count == 0)
                return skill;

            var modifiers = new List<RuntimeSkillModifier>();
            foreach (var e in effects)
                if (e.SkillModifier != null)
                    modifiers.Add(e.SkillModifier);

            if (modifiers.Count == 0) return skill;

            // ── Phase 1: Set (last modifier with a given key wins) ───────────
            double damageMultiplier = skill.DamageMultiplier;
            int cost = skill.Cost;
            bool isAoe = skill.IsAoe;
            int cooldown = skill.Cooldown;
            double baseHits = skill.BaseHits;

            foreach (var mod in modifiers)
            {
                if (mod.Set == null) continue;
                if (mod.Set.TryGetValue(ModifierVariable.DamageMultiplier, out var dm))
                    damageMultiplier = System.Convert.ToDouble(dm);
                if (mod.Set.TryGetValue(ModifierVariable.Cost, out var c))
                    cost = System.Convert.ToInt32(c);
                if (mod.Set.TryGetValue(ModifierVariable.IsAoe, out var aoe))
                    isAoe = System.Convert.ToBoolean(aoe);
                if (mod.Set.TryGetValue(ModifierVariable.Cooldown, out var cd))
                    cooldown = System.Convert.ToInt32(cd);
                if (mod.Set.TryGetValue(ModifierVariable.ExtraHits, out var eh))
                    baseHits = System.Convert.ToDouble(eh);
            }

            // ── Phase 2: Modify (sum all deltas) ─────────────────────────────
            foreach (var mod in modifiers)
            {
                if (mod.Modify == null) continue;
                if (mod.Modify.TryGetValue(ModifierVariable.DamageMultiplier, out var dm))
                    damageMultiplier += dm;
                if (mod.Modify.TryGetValue(ModifierVariable.Cost, out var c))
                    cost += (int)c;
                if (mod.Modify.TryGetValue(ModifierVariable.Cooldown, out var cd))
                    cooldown += (int)cd;
                if (mod.Modify.TryGetValue(ModifierVariable.ExtraHits, out var eh))
                    baseHits += eh;
            }

            // ── Phase 3: Add (append damage components to first effect) ──────
            var addedComponents = new List<DamageComponent>();
            foreach (var mod in modifiers)
                if (mod.AddDamagePerHit != null)
                    foreach (var comp in mod.AddDamagePerHit)
                        addedComponents.Add(comp);

            IReadOnlyList<SkillEffect> newEffects = skill.Effects;
            if (addedComponents.Count > 0 && skill.Effects.Count > 0)
            {
                var firstEffect = skill.Effects[0];
                var newComponents = new List<DamageComponent>(firstEffect.DamagePerHit);
                newComponents.AddRange(addedComponents);
                var newFirstEffect = firstEffect with { DamagePerHit = newComponents };
                var effectsList = new List<SkillEffect>(skill.Effects);
                effectsList[0] = newFirstEffect;
                newEffects = effectsList;
            }

            return skill with
            {
                DamageMultiplier = Math.Max(0.0, damageMultiplier),
                Cost = Math.Max(0, cost),
                IsAoe = isAoe,
                Cooldown = Math.Max(0, cooldown),
                BaseHits = Math.Max(0.5, baseHits),
                Effects = newEffects,
            };
        }

        /// <summary>Returns the flat (global, all-types) damage-dealt multiplier for a unit from active effects' StatModifiers.</summary>
        private double GetFlatDamageDealtMultiplier(string unitId) =>
            ResolveStatModifier(unitId, RuntimeStatKey.DamageDealtMultiplier, baseValue: 1.0);

        /// <summary>
        /// Returns the per-type outgoing damage multiplier for a unit and damage type from active effects.
        /// Feeds the "OutgoingTypeMult" pipeline step in <see cref="DamageCalc"/>.
        /// Base value is 1.0 (no change). Stacks multiplicatively across all active effect instances.
        /// </summary>
        private double GetOutgoingTypeMultiplier(string unitId, EffectType effectType)
        {
            if (!_activeEffects.TryGetValue(unitId, out var effects) || effects.Count == 0)
                return 1.0;
            double value = 1.0;
            foreach (var effect in effects)
            {
                if (effect.DamageDealtMultiplierByType == null) continue;
                if (effect.DamageDealtMultiplierByType.TryGetValue(effectType, out double m))
                    value *= m;
                // Physical sub-types inherit Physical outgoing multipliers.
                if ((effectType == EffectType.Blunt || effectType == EffectType.Slash)
                    && effect.DamageDealtMultiplierByType.TryGetValue(EffectType.Physical, out double mPhys))
                    value *= mPhys;
            }
            return value;
        }

        /// <summary>
        /// Returns the per-type incoming damage multiplier for a unit and damage type from active effects.
        /// Feeds the "IncomingTypeMult" pipeline step in <see cref="DamageCalc"/>.
        /// Base value is 1.0 (no change). Values less than 1.0 reduce damage taken.
        /// </summary>
        private double GetIncomingTypeMultiplier(string unitId, EffectType effectType)
        {
            if (!_activeEffects.TryGetValue(unitId, out var effects) || effects.Count == 0)
                return 1.0;
            double value = 1.0;
            foreach (var effect in effects)
            {
                if (effect.DamageTakenMultiplierByType == null) continue;
                if (effect.DamageTakenMultiplierByType.TryGetValue(effectType, out double m))
                    value *= m;
                // Physical sub-types inherit Physical incoming multipliers.
                if ((effectType == EffectType.Blunt || effectType == EffectType.Slash)
                    && effect.DamageTakenMultiplierByType.TryGetValue(EffectType.Physical, out double mPhys))
                    value *= mPhys;
            }
            return value;
        }

        /// <summary>Returns the effective damage-taken multiplier for a unit from active effects.</summary>
        private double GetDamageTakenMultiplier(string unitId) =>
            ResolveStatModifier(unitId, RuntimeStatKey.DamageTakenMultiplier, baseValue: 1.0);

        /// <summary>Returns the effective receiving-healing multiplier for a unit from active effects.</summary>
        private double GetReceivingHealingMultiplier(string unitId) =>
            ResolveStatModifier(unitId, RuntimeStatKey.ReceivingHealingMultiplier, baseValue: 1.0);

        /// <summary>Returns the effective receiving-barrier multiplier for a unit from active effects.</summary>
        private double GetReceivingBarrierMultiplier(string unitId) =>
            ResolveStatModifier(unitId, RuntimeStatKey.ReceivingBarrierMultiplier, baseValue: 1.0);

        /// <summary>
        /// Returns the effective resistance percentage for a unit and damage type, combining the
        /// unit's compiled base resistance with any runtime stat modifiers from active effects.
        /// </summary>
        private int GetEffectiveResistance(string unitId, EffectType effectType)
        {
            var unit = _allUnits.First(u => u.Id == unitId);
            // BattleUnit.GetResistance already stacks Physical for Blunt/Slash sub-types.
            int value = unit.GetResistance(effectType);
            if (_activeEffects.TryGetValue(unitId, out var effects))
                foreach (var effect in effects)
                {
                    if (effect.ResistanceModifierByType == null) continue;
                    if (effect.ResistanceModifierByType.TryGetValue(effectType, out int r))
                        value += r;
                    // Physical sub-types also inherit runtime Physical resistance modifiers.
                    if ((effectType == EffectType.Blunt || effectType == EffectType.Slash)
                        && effect.ResistanceModifierByType.TryGetValue(EffectType.Physical, out int rPhys))
                        value += rPhys;
                }
            return value;
        }

        /// <summary>
        /// Returns the effective penetration percentage for a unit and damage type, combining the
        /// unit's compiled base penetration with any runtime stat modifiers from active effects.
        /// </summary>
        private int GetEffectivePenetration(string unitId, EffectType effectType)
        {
            var unit = _allUnits.First(u => u.Id == unitId);
            // BattleUnit.GetPenetration already stacks Physical for Blunt/Slash sub-types.
            int value = unit.GetPenetration(effectType);
            if (_activeEffects.TryGetValue(unitId, out var effects))
                foreach (var effect in effects)
                {
                    if (effect.PenetrationModifierByType == null) continue;
                    if (effect.PenetrationModifierByType.TryGetValue(effectType, out int p))
                        value += p;
                    // Physical sub-types also inherit runtime Physical penetration modifiers.
                    if ((effectType == EffectType.Blunt || effectType == EffectType.Slash)
                        && effect.PenetrationModifierByType.TryGetValue(EffectType.Physical, out int pPhys))
                        value += pPhys;
                }
            return value;
        }

        /// <summary>
        /// Returns the effective disruption resistance for a unit, combining the compiled base value
        /// with any runtime stat modifiers from active effects.
        /// </summary>
        private int GetEffectiveDisruptionResistance(string unitId)
        {
            var unit = _allUnits.First(u => u.Id == unitId);
            return (int)ResolveStatModifier(unitId, RuntimeStatKey.DisruptionResistance,
                baseValue: unit.DisruptionResistance);
        }

        /// <summary>
        /// Returns the effective thermal protection for a unit, combining the compiled base value
        /// with any runtime stat modifiers from active effects.
        /// </summary>
        private int GetEffectiveThermalProtection(string unitId)
        {
            var unit = _allUnits.First(u => u.Id == unitId);
            return (int)ResolveStatModifier(unitId, RuntimeStatKey.ThermalProtection,
                baseValue: unit.ThermalProtection);
        }

        /// <summary>
        /// Returns the effective disruption penetration for a unit, combining the compiled base value
        /// with any runtime stat modifiers from active effects.
        /// </summary>
        private int GetEffectiveDisruptionPenetration(string unitId)
        {
            var unit = _allUnits.First(u => u.Id == unitId);
            return (int)ResolveStatModifier(unitId, RuntimeStatKey.DisruptionPenetration,
                baseValue: unit.DisruptionPenetration);
        }

        /// <summary>
        /// Resolves a single stat key by applying all active-effect modifiers in Set → Add → Multiply order.
        /// </summary>
        private double ResolveStatModifier(string unitId, RuntimeStatKey key, double baseValue)
        {
            if (!_activeEffects.TryGetValue(unitId, out var effects) || effects.Count == 0)
                return baseValue;

            double value = baseValue;

            // 1. Set: last modifier with this key wins.
            foreach (var effect in effects)
            {
                if (effect.StatModifiers == null) continue;
                foreach (var mod in effect.StatModifiers)
                    if (mod.StatKey == key && mod.Operation == ModifierOperation.Set)
                        value = mod.Value;
            }

            // 2. Add: sum all additive deltas.
            foreach (var effect in effects)
            {
                if (effect.StatModifiers == null) continue;
                foreach (var mod in effect.StatModifiers)
                    if (mod.StatKey == key && mod.Operation == ModifierOperation.Add)
                        value += mod.Value;
            }

            // 3. Multiply: apply all multiplicative factors.
            foreach (var effect in effects)
            {
                if (effect.StatModifiers == null) continue;
                foreach (var mod in effect.StatModifiers)
                    if (mod.StatKey == key && mod.Operation == ModifierOperation.Multiply)
                        value *= mod.Value;
            }

            return value;
        }

        /// <summary>
        /// Decrements the remaining duration for all active effects whose count-down condition matches
        /// the acting unit, and removes any effects that have expired.
        /// </summary>
        private IReadOnlyList<BattleEvent> TickActiveEffects(string actorId)
        {
            var events = new List<BattleEvent>();

            foreach (var unitId in new List<string>(_activeEffects.Keys))
            {
                if (!_activeEffects.TryGetValue(unitId, out var effectList)) continue;

                for (int i = effectList.Count - 1; i >= 0; i--)
                {
                    var effect = effectList[i];
                    bool shouldTick = effect.DurationKind switch
                    {
                        EffectDurationKind.ForTargetTurns => effect.TargetUnitId == actorId,
                        EffectDurationKind.ForSourceTurns => effect.SourceUnitId == actorId,
                        EffectDurationKind.UntilNextAction => effect.TargetUnitId == actorId,
                        _ => false,
                    };

                    if (!shouldTick) continue;

                    int newDuration = effect.RemainingDuration - 1;
                    if (newDuration <= 0)
                    {
                        effectList.RemoveAt(i);
                        var targetUnit = _allUnits.FirstOrDefault(u => u.Id == unitId);
                        string targetName = targetUnit?.Name ?? unitId;
                        events.Add(AddEvent(actorId, $"{effect.Name} fades from {targetName}.", "status"));
                    }
                    else
                    {
                        effectList[i] = effect with { RemainingDuration = newDuration };
                    }
                }
            }

            return events;
        }

        /// <summary>Builds the UI-facing active-effect view list for a unit. Null when none are active.</summary>
        private IReadOnlyList<ActiveEffectView>? BuildActiveEffectViews(string unitId)
        {
            if (!_activeEffects.TryGetValue(unitId, out var effects) || effects.Count == 0)
                return null;
            var views = new List<ActiveEffectView>(effects.Count);
            foreach (var e in effects)
                views.Add(new ActiveEffectView(e.DefinitionId, e.Name, e.RemainingDuration, e.Stacks,
                    IsDebuff: e.Alignment == EffectAlignment.Debuff));
            return views;
        }

        private static ActiveEffectInstance? FindExistingInstance(List<ActiveEffectInstance> list, string definitionId)
        {
            foreach (var e in list)
                if (e.DefinitionId == definitionId)
                    return e;
            return null;
        }

        /// <summary>Returns true when the unit currently has an active effect with the given definition ID.</summary>
        private bool HasActiveEffect(string unitId, string definitionId) =>
            _activeEffects.TryGetValue(unitId, out var list)
            && list.Exists(e => e.DefinitionId == definitionId);

        /// <summary>Removes all active effect instances with the given definition ID from a unit.</summary>
        private void RemoveActiveEffect(string unitId, string definitionId)
        {
            if (_activeEffects.TryGetValue(unitId, out var list))
                list.RemoveAll(e => e.DefinitionId == definitionId);
        }

        // ── Dispel helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Collects all effect IDs eligible for dispelling from a unit.
        /// Includes active effect instances with matching alignment plus bar-derived
        /// disruption statuses (dizzy, stunned) when debuffs are targeted.
        /// </summary>
        private List<string> CollectDispelCandidates(string unitId, EffectAlignment? alignment)
        {
            var candidates = new List<string>();
            bool wantDebuff = alignment == null || alignment == EffectAlignment.Debuff;
            bool wantBuff = alignment == null || alignment == EffectAlignment.Buff;

            // Active effect instances.
            if (_activeEffects.TryGetValue(unitId, out var effects))
                foreach (var e in effects)
                {
                    bool isDebuff = e.Alignment == EffectAlignment.Debuff;
                    if ((isDebuff && wantDebuff) || (!isDebuff && wantBuff))
                        candidates.Add(e.DefinitionId);
                }

            // Disruption bar-derived statuses (not tracked as ActiveEffectInstances).
            if (wantDebuff)
            {
                bool isStunned = _stunnedUnits.Contains(unitId);
                int disruptionBar = GetBar(unitId, DisruptionSystem.BarDisruption);
                if (isStunned)
                    candidates.Add(DisruptionSystem.StatusStunned);
                else if (disruptionBar >= DisruptionSystem.DizzyThreshold)
                    candidates.Add(DisruptionSystem.StatusDizzy);
                else if (disruptionBar > 0)
                    candidates.Add(DisruptionSystem.StatusShaken);
            }

            return candidates;
        }

        /// <summary>
        /// Returns a display name for an effect ID — uses the active instance name when available,
        /// falling back to the raw ID for bar-derived statuses (dizzy, stunned).
        /// </summary>
        private string GetEffectDisplayName(string unitId, string effectId)
        {
            if (_activeEffects.TryGetValue(unitId, out var effects))
            {
                var instance = effects.Find(e => e.DefinitionId == effectId);
                if (instance != null)
                    return instance.Name;
            }
            // Bar-derived effects: capitalize the ID as a fallback.
            return char.ToUpperInvariant(effectId[0]) + effectId.Substring(1);
        }

        /// <summary>
        /// Removes the named effect from a unit.
        /// Bar-linked effects (slow, frozen, burning, dizzy, stunned) also reset
        /// their backing bar so the status cannot immediately reapply.
        /// </summary>
        private void DispelEffect(string unitId, string effectId)
        {
            // Thermal cold bar-linked effects: reset the cold bar and sync.
            if (effectId == ThermalSystem.StatusChilled
                || effectId == ThermalSystem.StatusSlow
                || effectId == ThermalSystem.StatusFrozen)
            {
                _bars[unitId][ThermalSystem.BarCold] = 0;
                SyncThermalActiveEffects(unitId);
                // frozen is not handled by SyncThermalActiveEffects — remove it explicitly.
                if (effectId == ThermalSystem.StatusFrozen)
                    RemoveActiveEffect(unitId, ThermalSystem.StatusFrozen);
            }
            // Thermal burn bar-linked effects: reset the burn bar and sync.
            else if (effectId == ThermalSystem.StatusHeated || effectId == ThermalSystem.StatusBurning)
            {
                _bars[unitId][ThermalSystem.BarBurn] = 0;
                SyncThermalActiveEffects(unitId);
            }
            // Disruption bar-derived statuses: reset the bar (and clear the stunned set if stunned).
            else if (effectId == DisruptionSystem.StatusStunned)
            {
                _stunnedUnits.Remove(unitId);
                _bars[unitId][DisruptionSystem.BarDisruption] = 0;
            }
            else if (effectId == DisruptionSystem.StatusDizzy || effectId == DisruptionSystem.StatusShaken)
            {
                _bars[unitId][DisruptionSystem.BarDisruption] = 0;
            }
            // Bleed bar-linked effects: reset the bleed bar and sync.
            else if (effectId == BleedSystem.StatusScratched || effectId == BleedSystem.StatusBleeding)
            {
                _bars[unitId][BleedSystem.BarBleed] = 0;
                SyncBleedActiveEffects(unitId);
            }
            // Regular active effects: remove directly.
            else
            {
                RemoveActiveEffect(unitId, effectId);
            }
        }

        /// <summary>
        /// Applies or removes the <c>slow</c> and <c>burning</c> active effects based on the
        /// unit's current thermal bar values. Called after any thermal bar change (buildup or decay).
        /// Does not touch the <c>frozen</c> effect — freeze is applied on trigger and consumed on
        /// the unit's next turn, independent of bar values.
        /// </summary>
        private void SyncThermalActiveEffects(string unitId)
        {
            int coldBar = GetBar(unitId, ThermalSystem.BarCold);
            int burnBar = GetBar(unitId, ThermalSystem.BarBurn);
            bool isFrozen = HasActiveEffect(unitId, ThermalSystem.StatusFrozen);

            // Chilled: active when cold is building but below the slow threshold and not frozen.
            bool shouldBeChilled = coldBar > 0 && coldBar < ThermalSystem.SlowThreshold && !isFrozen;
            if (shouldBeChilled && !HasActiveEffect(unitId, ThermalSystem.StatusChilled))
                ApplyActiveEffect(unitId, _chilledDef, unitId);
            else if (!shouldBeChilled)
                RemoveActiveEffect(unitId, ThermalSystem.StatusChilled);

            // Slow: active when cold >= threshold AND the unit is not already frozen.
            bool shouldBeSlow = coldBar >= ThermalSystem.SlowThreshold && !isFrozen;
            if (shouldBeSlow && !HasActiveEffect(unitId, ThermalSystem.StatusSlow))
                ApplyActiveEffect(unitId, _slowDef, unitId);
            else if (!shouldBeSlow)
                RemoveActiveEffect(unitId, ThermalSystem.StatusSlow);

            // Heated: active when burn is building but below the burning threshold.
            bool shouldBeHeated = burnBar > 0 && burnBar < ThermalSystem.BurningThreshold;
            if (shouldBeHeated && !HasActiveEffect(unitId, ThermalSystem.StatusHeated))
                ApplyActiveEffect(unitId, _heatedDef, unitId);
            else if (!shouldBeHeated)
                RemoveActiveEffect(unitId, ThermalSystem.StatusHeated);

            // Burning: active when burn >= threshold (independent of cold/frozen).
            bool shouldBeBurning = burnBar >= ThermalSystem.BurningThreshold;
            if (shouldBeBurning && !HasActiveEffect(unitId, ThermalSystem.StatusBurning))
                ApplyActiveEffect(unitId, _burningDef, unitId);
            else if (!shouldBeBurning)
                RemoveActiveEffect(unitId, ThermalSystem.StatusBurning);
        }
    }
}
