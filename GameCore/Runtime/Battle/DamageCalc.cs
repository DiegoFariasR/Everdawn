#nullable enable
using System;
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Runs the damage pipeline for one hit.
    /// <para>
    /// Each step transforms the current value and records <c>ValueBefore</c> and
    /// <c>ValueAfter</c>. The ordered <see cref="DamageResult.Steps"/> list is the
    /// full audit trail — use it to debug or predict any hit precisely.
    /// </para>
    /// <para>
    /// The full flow diagram and traceability table live in
    /// <c>Docs/Design/damage-pipeline.md</c>. Keep the <c>DamageStep</c> name
    /// strings, the diagram, and that table in sync whenever a layer changes.
    /// </para>
    /// <para>
    /// To add a new layer: append a step block below the existing ones in <see cref="Compute"/>.
    /// Read the current <c>value</c>, compute the transformed result, record a new
    /// <see cref="DamageStep"/>, then update <c>value</c>.
    /// </para>
    /// </summary>
    public static class DamageCalc
    {
        /// <summary>
        /// Runs every pipeline layer in order and returns the full <see cref="DamageResult"/>.
        /// </summary>
        public static DamageResult Compute(
            BattleUnit actor,
            BattleUnit target,
            EffectType effectType,
            double skillMultiplier,
            double extraMultiplier,
            Random rng)
        {
            var steps = new List<DamageStep>();

            // ── Layer 1: Base ────────────────────────────────────────────────
            // Roll damage from the attacker's stat, skill multiplier, and variance.
            // ValueBefore = flat base stat (for reference). ValueAfter = rolled value.
            int baseAttack = actor.GetBaseAttack(effectType);
            int variance = Math.Max(1, baseAttack / 5);
            int rolled = Math.Max(
                0,
                (int)(baseAttack * skillMultiplier * extraMultiplier) + rng.Next(-variance, variance + 1)
            );
            steps.Add(new DamageStep("Base", baseAttack, rolled));
            int value = rolled;

            // ── Layer 2: Resistance ──────────────────────────────────────────
            // Reduce damage by the defender's effective resistance (raw minus penetration).
            // Capped at 90: even full immunity still takes ≥10% damage.
            // Negative = weakness (extra damage). No floor below the cap.
            int resistance = Math.Min(90, target.GetResistance(effectType) - actor.GetPenetration(effectType));
            int afterResistance = Math.Max(0, (int)(value * (1.0 - resistance / 100.0)));
            steps.Add(new DamageStep("Resistance", value, afterResistance));
            value = afterResistance;

            // ── Add future layers here ───────────────────────────────────────

            return new DamageResult(effectType, steps);
        }

        /// <summary>
        /// Runs the damage pipeline for each component in <paramref name="components"/> and returns
        /// one <see cref="DamageResult"/> per component.
        /// Components with a null <see cref="DamageComponent.DamageType"/> are skipped (heal-only components).
        /// </summary>
        /// <param name="getEffectiveResistance">
        /// Optional override for the resistance lookup. When provided, called instead of
        /// <c>target.GetResistance(type)</c> so that runtime active-effect resistance modifiers
        /// are applied. When null, falls back to the unit's compiled base resistances.
        /// </param>
        /// <param name="getEffectivePenetration">
        /// Optional override for the penetration lookup. When provided, called instead of
        /// <c>actor.GetPenetration(type)</c> so that runtime active-effect penetration modifiers
        /// are applied. When null, falls back to the actor's compiled base penetrations.
        /// </param>
        /// <param name="getOutgoingTypeMult">
        /// Optional resolver for the actor's per-type outgoing damage multiplier (the "OutgoingTypeMult" step).
        /// Called with the component's damage type; the returned value multiplies the damage for that type
        /// (e.g. 1.3 = actor deals 30% more). Stacks multiplicatively. Null = 1.0 for all types (no change).
        /// </param>
        /// <param name="getIncomingTypeMult">
        /// Optional resolver for the target's per-type incoming damage multiplier (the "IncomingTypeMult" step).
        /// Called with the component's damage type; the returned value multiplies the damage for that type
        /// (e.g. 0.8 = target takes 20% less). Stacks multiplicatively. Null = 1.0 for all types (no change).
        /// </param>
        /// <param name="attackerOutputMult">
        /// Flat attacker-side output multiplier applied after per-type modifiers (Layer 5: "AttackerOutput").
        /// Use this for status penalties such as Dizzy (×0.8). Default 1.0 = no change.
        /// </param>
        /// <param name="defenderDamageTakenMult">
        /// Flat defender-side damage-taken multiplier applied last (Layer 6: "DamageTaken").
        /// Sourced from the target's active-effect <c>DamageTakenMultiplier</c> stat. Default 1.0 = no change.
        /// </param>
        public static IReadOnlyList<DamageResult> Compute(
            BattleUnit actor,
            BattleUnit target,
            IReadOnlyList<DamageComponent> components,
            double damageMultiplier,
            double extraMultiplier,
            Random rng,
            Func<EffectType, int>? getEffectiveResistance = null,
            Func<EffectType, int>? getEffectivePenetration = null,
            Func<EffectType, double>? getOutgoingTypeMult = null,
            Func<EffectType, double>? getIncomingTypeMult = null,
            double attackerOutputMult = 1.0,
            double defenderDamageTakenMult = 1.0)
        {
            var results = new List<DamageResult>();
            foreach (var component in components)
            {
                if (!component.DamageType.HasValue) continue;

                var steps = new List<DamageStep>();

                // ── Layer 1: Base ────────────────────────────────────────────────
                // Sum each stat contribution, apply damageMultiplier + extraMultiplier, add variance.
                double statBase = 0;
                foreach (var s in component.Scaling)
                    statBase += actor.GetStat(s.Stat) * s.Scale;
                int baseValue = (int)statBase;
                int variance = Math.Max(1, baseValue / 5);
                int rolled = Math.Max(
                    0,
                    (int)(statBase * damageMultiplier * extraMultiplier) + rng.Next(-variance, variance + 1)
                );
                steps.Add(new DamageStep("Base", baseValue, rolled));
                int value = rolled;

                // ── Layer 2: Resistance ──────────────────────────────────────────
                // Use getEffectiveResistance if supplied (includes runtime stat modifiers);
                // fall back to the unit's compiled base resistance otherwise.
                // Subtract the actor's penetration (compiled or runtime) from the resistance.
                // Capped at 90: even full immunity still takes ≥10% damage.
                int resistance = Math.Min(90, (getEffectiveResistance != null
                    ? getEffectiveResistance(component.DamageType.Value)
                    : target.GetResistance(component.DamageType.Value))
                    - (getEffectivePenetration != null
                        ? getEffectivePenetration(component.DamageType.Value)
                        : actor.GetPenetration(component.DamageType.Value)));
                int afterResistance = Math.Max(0, (int)(value * (1.0 - resistance / 100.0)));
                steps.Add(new DamageStep("Resistance", value, afterResistance));
                value = afterResistance;

                // ── Layer 3: OutgoingTypeMult ────────────────────────────────────
                // Multiply the current damage by the actor's per-type outgoing damage multiplier
                // from active effects (e.g. Attack Up → ×1.3 for all types).
                // Base value is 1.0 (no change). Values > 1.0 increase damage; < 1.0 decrease it.
                if (getOutgoingTypeMult != null)
                {
                    double outgoingMult = getOutgoingTypeMult(component.DamageType.Value);
                    if (outgoingMult != 1.0)
                    {
                        int afterOutgoingTypeMult = Math.Max(0, (int)(value * outgoingMult));
                        steps.Add(new DamageStep("OutgoingTypeMult", value, afterOutgoingTypeMult));
                        value = afterOutgoingTypeMult;
                    }
                }

                // ── Layer 4: IncomingTypeMult ────────────────────────────────────
                // Multiply damage by the target's per-type incoming damage multiplier
                // from active effects (e.g. Defense Up → ×0.75 for all types).
                // Values < 1.0 reduce damage; values > 1.0 amplify it.
                if (getIncomingTypeMult != null)
                {
                    double incomingMult = getIncomingTypeMult(component.DamageType.Value);
                    if (incomingMult != 1.0)
                    {
                        int afterIncomingTypeMult = Math.Max(0, (int)(value * incomingMult));
                        steps.Add(new DamageStep("IncomingTypeMult", value, afterIncomingTypeMult));
                        value = afterIncomingTypeMult;
                    }
                }

                // ── Layer 5: AttackerOutput ─────────────────────────────────────
                // Flat attacker-side output modifier. Applied after per-type multipliers.
                // Primary use: Dizzy status (×0.8). Skipped when equal to 1.0.
                if (attackerOutputMult != 1.0)
                {
                    int afterAttackerOutput = Math.Max(0, (int)(value * attackerOutputMult));
                    steps.Add(new DamageStep("AttackerOutput", value, afterAttackerOutput));
                    value = afterAttackerOutput;
                }

                // ── Layer 6: DamageTaken ─────────────────────────────────────────
                // Flat defender-side damage-taken modifier from active effects.
                // Runs last so it stacks over all other layers. Skipped when equal to 1.0.
                if (defenderDamageTakenMult != 1.0)
                {
                    int afterDamageTaken = Math.Max(0, (int)(value * defenderDamageTakenMult));
                    steps.Add(new DamageStep("DamageTaken", value, afterDamageTaken));
                    value = afterDamageTaken;
                }

                // ── Add future layers here ───────────────────────────────────────

                results.Add(new DamageResult(component.DamageType.Value, steps, component.BuildupPower));
            }
            return results;
        }
    }
}
