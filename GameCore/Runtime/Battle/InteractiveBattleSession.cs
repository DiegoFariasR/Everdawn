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
        // Units that will lose their next turn due to the frozen status effect.
        private readonly HashSet<string> _frozenUnits = new HashSet<string>();
        // Units that will lose their next turn due to the stunned status effect.
        private readonly HashSet<string> _stunnedUnits = new HashSet<string>();
        // Active runtime effects: unitId → list of instances currently on that unit.
        private readonly Dictionary<string, List<ActiveEffectInstance>> _activeEffects =
            new Dictionary<string, List<ActiveEffectInstance>>();
        // Counter for generating unique runtime instance IDs within this session.
        private int _nextEffectId;

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

            // Start-of-turn phase.
            var newEvents = new List<BattleEvent>(StartOfTurn(actor));

            // If the player is frozen, their chosen action is skipped and the turn is consumed.
            if (_frozenUnits.Contains(actor.Id))
            {
                _frozenUnits.Remove(actor.Id);
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
            if (skill.IsAoe)
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

            // Start-of-turn phase.
            var newEvents = new List<BattleEvent>(StartOfTurn(actor));

            if (_frozenUnits.Contains(actor.Id))
            {
                _frozenUnits.Remove(actor.Id);
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

            var skill = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive && GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor)).LastOrDefault();
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
            if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
            newEvents.AddRange(AutoAdvance());
            return BuildResponse(newEvents);
        }

        private BattleResponse HandleAdvanceOneTurn(AdvanceOneTurnRequest _)
        {
            if (!_started) throw new InvalidOperationException("Session not started.");
            if (_isOver) return BuildResponse(Array.Empty<BattleEvent>());

            var actor = _turnOrder[_turnIndex];

            // Start-of-turn phase.
            var newEvents = new List<BattleEvent>(StartOfTurn(actor));
            if (_isOver) return BuildResponse(newEvents);

            if (_frozenUnits.Contains(actor.Id))
            {
                // Consume the frozen turn: skip the action and advance.
                _frozenUnits.Remove(actor.Id);
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

            var skill = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive && GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor)).LastOrDefault();
            if (skill == null)
            {
                newEvents.Add(AddEvent(actor.Id, $"{actor.Name} waits.", "status"));
                if (AdvanceTurn()) newEvents.AddRange(StartOfRound());
                return BuildResponse(newEvents);
            }

            var targets = ResolveAutoTargets(actor, skill);
            if (targets.Count == 0) { CheckEnd(); return BuildResponse(newEvents); }

            newEvents.AddRange(ExecuteAction(actor, targets, skill));
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
                    AvailableSkillIds: skills.Where(s => GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor)).Select(s => s.Id).ToArray(),
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
                bool isFrozen = _frozenUnits.Contains(actor.Id);
                bool isStunned = _stunnedUnits.Contains(actor.Id);

                // Stop when it is a living, non-frozen, non-stunned player's turn — they need to provide input.
                if (!isFrozen && !isStunned && actor.Team == "player") break;

                // Process start-of-turn effects for this actor (burn DOT, thermal decay, disruption decay).
                produced.AddRange(StartOfTurn(actor));
                if (_isOver) break;

                if (isFrozen)
                {
                    // Consume the frozen turn: skip the action and advance.
                    _frozenUnits.Remove(actor.Id);
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
                var skill = actor.ResolvedSkills.Where(s => s.Category != SkillCategory.Passive && GetBar(actor.Id, "mp") >= s.Cost && GetCooldown(s.Id) <= 0 && s.MeetsRequirements(actor)).LastOrDefault();
                if (skill == null)
                {
                    produced.Add(AddEvent(actor.Id, $"{actor.Name} waits.", "status"));
                    if (AdvanceTurn()) produced.AddRange(StartOfRound());
                    continue;
                }

                var targets = ResolveAutoTargets(actor, skill);
                if (targets.Count == 0) { CheckEnd(); break; }

                produced.AddRange(ExecuteAction(actor, targets, skill));
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

            // Resolve the effective skill for this action: applies any runtime skill modifiers
            // from active effects on the actor. The base compiled skill is never mutated.
            var effectiveSkill = ResolveEffectiveSkill(actor, skill);

            // Consume the skill's cost from the unit's mana bar, if they have one.
            if (_bars[actor.Id].ContainsKey("mp"))
                _bars[actor.Id]["mp"] = Math.Max(0, _bars[actor.Id]["mp"] - effectiveSkill.Cost);

            // Determine event type from skill modifier
            string evType = (effectiveSkill.IsHeal || effectiveSkill.IsShield || effectiveSkill.IsRestoreBar) ? "skill"
                           : effectiveSkill.IsBasic ? "attack" : effectiveSkill.IsUltimate ? "soulburn" : "skill";

            // Focus empowerment: fires when focus is full and the actor uses a non-basic offensive skill
            bool isFocusEmpowered = actor.HasTrait(BattleTrait.Focus)
                && GetBar(actor.Id, "focus") == 100
                && !effectiveSkill.IsBasic
                && !effectiveSkill.IsHeal
                && !effectiveSkill.IsShield
                && !effectiveSkill.IsRestoreBar
                && !effectiveSkill.IsApplyEffect;
            if (isFocusEmpowered)
            {
                _bars[actor.Id]["focus"] = 50;
                produced.Add(AddEvent(actor.Id, $"{actor.Name}'s Focus empowers their attack!", "skill"));
            }
            // Fury empowerment: fires when fury is full and the actor uses a non-basic offensive skill
            bool isFuryEmpowered = actor.HasTrait(BattleTrait.Fury)
                && GetBar(actor.Id, "fury") == 100
                && !effectiveSkill.IsBasic
                && !effectiveSkill.IsHeal
                && !effectiveSkill.IsShield
                && !effectiveSkill.IsRestoreBar
                && !effectiveSkill.IsApplyEffect;
            if (isFuryEmpowered)
            {
                _bars[actor.Id]["fury"] = 0;
                produced.Add(AddEvent(actor.Id, $"{actor.Name}'s Fury empowers their attack!", "skill"));
            }
            // Flat (global) DamageDealtMultiplier from active effects stacks with focus/fury empowerment.
            double empowerMult = GetFlatDamageDealtMultiplier(actor.Id);
            if (isFocusEmpowered) empowerMult *= 1.5;
            if (isFuryEmpowered) empowerMult *= 1.5;

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
                bool actorIsSlow = GetBar(actor.Id, ThermalSystem.BarCold) >= ThermalSystem.SlowThreshold
                                   && !_frozenUnits.Contains(actor.Id);
                effectiveHits = (effectiveSkill.IsHeal || effectiveSkill.IsShield || effectiveSkill.IsRestoreBar) ? 1 : ThermalSystem.ResolveAgiHits(actor.Agi, actorIsSlow);
                perHitHitsMult = 1.0;
            }

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
                        // Pass effective resistance, penetration, and per-type damage dealt multiplier
                        // resolvers so runtime modifiers are applied.
                        var hitResults = DamageCalc.Compute(actor, target, components, effectiveSkill.DamageMultiplier,
                            empowerMult * perHitHitsMult, _rng,
                            et => GetEffectiveResistance(target.Id, et),
                            et => GetEffectivePenetration(actor.Id, et),
                            et => GetDamageDealtMultiplierForType(actor.Id, et),
                            et => GetDamageTakenMultiplierForType(target.Id, et));
                        int totalDamage = 0;
                        foreach (var r in hitResults) totalDamage += r.FinalDamage;

                        // Dizzy: reduce the actor's final damage output by 20%.
                        totalDamage = DisruptionSystem.ApplyDizzyReduction(totalDamage, actorIsDizzy);

                        // DamageTakenMultiplier (flat, from StatModifiers) from target's active effects.
                        // Applied after dizzy and after per-type taken multiplier.
                        double damageTakenMult = GetDamageTakenMultiplier(target.Id);
                        totalDamage = (int)(totalDamage * damageTakenMult);

                        var primaryType = effectiveSkill.PrimaryEffectType;

                        // Barrier absorbs damage before HP.
                        int barrierCurrent = GetBar(target.Id, "barrier");
                        int barrierAbsorbed = Math.Min(barrierCurrent, totalDamage);
                        if (barrierAbsorbed > 0)
                            _bars[target.Id]["barrier"] = barrierCurrent - barrierAbsorbed;
                        _hp[target.Id] = Math.Max(0, _hp[target.Id] - (totalDamage - barrierAbsorbed));

                        string hitLabel = effectiveHits > 1 ? $" (hit {i + 1}/{effectiveHits})" : "";
                        produced.Add(AddEvent(actor.Id,
                            effectiveSkill.IsAoe
                                ? $"  \u2192 {target.Name} takes {totalDamage} damage{hitLabel}."
                                : $"{actor.Name} uses {effectiveSkill.Name} on {target.Name} for {totalDamage} damage{hitLabel}.",
                            evType, target.Id, totalDamage,
                            skillId: skill.Id, effectType: primaryType,
                            hitIndex: i, totalHits: effectiveHits));

                        // Focus: actor gains 10 per offensive hit; target loses 10 per incoming hit
                        if (actor.HasTrait(BattleTrait.Focus))
                            _bars[actor.Id]["focus"] = Math.Min(100, GetBar(actor.Id, "focus") + 10);
                        if (target.HasTrait(BattleTrait.Focus))
                            _bars[target.Id]["focus"] = Math.Max(0, GetBar(target.Id, "focus") - 10);
                        // Fury: target gains 10–20 per incoming hit
                        if (target.HasTrait(BattleTrait.Fury))
                            _bars[target.Id]["fury"] = Math.Min(100, GetBar(target.Id, "fury") + _rng.Next(10, 21));

                        // Thermal buildup: fire and cold hits build opposing bars on the target.
                        produced.AddRange(ApplyThermalBuildup(target, hitResults));

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
            // Fury: actor gains 10–50 per action (excludes heals, shields, and bar restores)
            if (actor.HasTrait(BattleTrait.Fury) && !effectiveSkill.IsHeal && !effectiveSkill.IsShield && !effectiveSkill.IsRestoreBar && !effectiveSkill.IsApplyEffect)
                _bars[actor.Id]["fury"] = Math.Min(100, GetBar(actor.Id, "fury") + _rng.Next(10, 51));

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
                    && !_frozenUnits.Contains(t.Id)                 // CC suppresses reactions
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
                    et => GetDamageDealtMultiplierForType(reactor.Id, et),
                    et => GetDamageTakenMultiplierForType(target.Id, et));

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

            // Disruption bar decay.
            int disruptionBar = GetBar(actor.Id, DisruptionSystem.BarDisruption);
            int newDisruption = DisruptionSystem.ApplyDecay(disruptionBar);
            if (newDisruption != disruptionBar) _bars[actor.Id][DisruptionSystem.BarDisruption] = newDisruption;

            return events;
        }

        /// <summary>
        /// Applies thermal buildup to <paramref name="target"/> from fire and cold hits in
        /// <paramref name="hitResults"/>. Generates events for freeze triggers.
        /// Uses runtime resistance modifiers from active effects when computing buildup.
        /// </summary>
        private IReadOnlyList<BattleEvent> ApplyThermalBuildup(BattleUnit target, IReadOnlyList<DamageResult> hitResults)
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
                        _frozenUnits.Add(target.Id);
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
            }
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

        /// <summary>
        /// Returns the active status effect IDs for a unit, or null when none are active.
        /// Combines thermal (slow/frozen/burning), disruption (dizzy/stunned), and runtime active-effect IDs.
        /// </summary>
        private IReadOnlyList<string>? BuildStatusEffects(string unitId)
        {
            if (_hp[unitId] <= 0) return null;

            int coldBar = GetBar(unitId, ThermalSystem.BarCold);
            int burnBar = GetBar(unitId, ThermalSystem.BarBurn);
            bool isFrozen = _frozenUnits.Contains(unitId);
            var thermalEffects = ThermalSystem.GetThermalStatusEffects(coldBar, burnBar, isFrozen);

            int disruptionBar = GetBar(unitId, DisruptionSystem.BarDisruption);
            bool isStunned = _stunnedUnits.Contains(unitId);
            var disruptionEffects = DisruptionSystem.GetDisruptionStatusEffects(disruptionBar, isStunned);

            // Include the definition ID of each active runtime effect so the client can display icons.
            bool hasActiveEffects = _activeEffects.TryGetValue(unitId, out var effects) && effects.Count > 0;

            if (thermalEffects.Count == 0 && disruptionEffects.Count == 0 && !hasActiveEffects) return null;

            var all = new List<string>(thermalEffects);
            all.AddRange(disruptionEffects);
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
                PenetrationModifierByType: definition.PenetrationModifierByType
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
        /// Base value is 1.0 (no change). Stacks multiplicatively across all active effect instances.
        /// </summary>
        private double GetDamageDealtMultiplierForType(string unitId, EffectType effectType)
        {
            if (!_activeEffects.TryGetValue(unitId, out var effects) || effects.Count == 0)
                return 1.0;
            double value = 1.0;
            foreach (var effect in effects)
                if (effect.DamageDealtMultiplierByType != null && effect.DamageDealtMultiplierByType.TryGetValue(effectType, out double m))
                    value *= m;
            return value;
        }

        /// <summary>
        /// Returns the effective per-type damage taken multiplier for a unit and damage type from active effects.
        /// Base value is 1.0 (no change). Values less than 1.0 reduce damage taken.
        /// </summary>
        private double GetDamageTakenMultiplierForType(string unitId, EffectType effectType)
        {
            if (!_activeEffects.TryGetValue(unitId, out var effects) || effects.Count == 0)
                return 1.0;
            double value = 1.0;
            foreach (var effect in effects)
                if (effect.DamageTakenMultiplierByType != null && effect.DamageTakenMultiplierByType.TryGetValue(effectType, out double m))
                    value *= m;
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
            int value = unit.GetResistance(effectType);
            if (_activeEffects.TryGetValue(unitId, out var effects))
                foreach (var effect in effects)
                    if (effect.ResistanceModifierByType != null && effect.ResistanceModifierByType.TryGetValue(effectType, out int r))
                        value += r;
            return value;
        }

        /// <summary>
        /// Returns the effective penetration percentage for a unit and damage type, combining the
        /// unit's compiled base penetration with any runtime stat modifiers from active effects.
        /// </summary>
        private int GetEffectivePenetration(string unitId, EffectType effectType)
        {
            var unit = _allUnits.First(u => u.Id == unitId);
            int value = unit.GetPenetration(effectType);
            if (_activeEffects.TryGetValue(unitId, out var effects))
                foreach (var effect in effects)
                    if (effect.PenetrationModifierByType != null && effect.PenetrationModifierByType.TryGetValue(effectType, out int p))
                        value += p;
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
                views.Add(new ActiveEffectView(e.DefinitionId, e.Name, e.RemainingDuration, e.Stacks));
            return views;
        }

        private static ActiveEffectInstance? FindExistingInstance(List<ActiveEffectInstance> list, string definitionId)
        {
            foreach (var e in list)
                if (e.DefinitionId == definitionId)
                    return e;
            return null;
        }
    }
}
