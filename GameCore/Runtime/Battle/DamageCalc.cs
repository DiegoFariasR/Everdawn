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
        /// <param name="getEffectiveDamageTakenMultiplier">
        /// Optional override for the per-type damage taken multiplier lookup on the target.
        /// When provided, called with the component's damage type and the returned value multiplies
        /// the damage for that type (e.g. 0.8 = target takes 20% less). Stacks multiplicatively.
        /// When null, no per-type taken multiplier is applied (equivalent to 1.0 for all types).
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
            Func<EffectType, double>? getEffectiveDamageDealtMultiplier = null,
            Func<EffectType, double>? getEffectiveDamageTakenMultiplier = null)
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

                // ── Layer 3: TypeMultiplier ──────────────────────────────────────
                // Multiply the current damage by the actor's per-type damage dealt multiplier.
                // Base value is 1.0 (no change). Values > 1.0 increase damage; < 1.0 decrease it.
                if (getEffectiveDamageDealtMultiplier != null)
                {
                    double typeMult = getEffectiveDamageDealtMultiplier(component.DamageType.Value);
                    if (typeMult != 1.0)
                    {
                        int afterTypeMult = Math.Max(0, (int)(value * typeMult));
                        steps.Add(new DamageStep("TypeMultiplier", value, afterTypeMult));
                        value = afterTypeMult;
                    }
                }

                // ── Layer 4: DamageTakenTypeMultiplier ───────────────────────────
                // Multiply damage by the target's per-type damage taken multiplier.
                // Values < 1.0 reduce damage (e.g. 0.8 = target takes 20% less).
                if (getEffectiveDamageTakenMultiplier != null)
                {
                    double takenMult = getEffectiveDamageTakenMultiplier(component.DamageType.Value);
                    if (takenMult != 1.0)
                    {
                        int afterTakenMult = Math.Max(0, (int)(value * takenMult));
                        steps.Add(new DamageStep("DamageTakenTypeMultiplier", value, afterTakenMult));
                        value = afterTakenMult;
                    }
                }

                // ── Add future layers here ───────────────────────────────────────

                results.Add(new DamageResult(component.DamageType.Value, steps, component.BuildupPower));
            }
            return results;
        }
    }
}
