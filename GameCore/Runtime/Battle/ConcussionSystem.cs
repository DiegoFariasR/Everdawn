using System;
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Central helper for the concussion buildup bar system.
    /// <para>
    /// Concussion represents blunt physical impact. It is fed exclusively by
    /// <see cref="EffectType.Blunt"/> damage components that carry a non-zero
    /// <see cref="DamageComponent.BuildupPower"/>.
    /// </para>
    /// <para>
    /// Thresholds:
    /// <list type="bullet">
    ///   <item>Concussion ≥ 50 → <c>dazed</c> (actor's final damage output reduced by 20 %)</item>
    ///   <item>Concussion = 100 → <c>concussed</c> for 1 turn, bar retained at <see cref="ConcussedRetainedBar"/></item>
    /// </list>
    /// </para>
    /// <para>
    /// Resistance against concussion buildup is drawn from the target's physical resistance
    /// (capped at 90 % so that even a fully-resistant unit still accumulates some buildup).
    /// </para>
    /// </summary>
    public static class ConcussionSystem
    {
        // ── Tuning constants ─────────────────────────────────────────────────

        /// <summary>Maximum value for the concussion bar.</summary>
        public const int MaxBar = 100;

        /// <summary>Concussion bar value at which the <c>dazed</c> debuff is applied.</summary>
        public const int DazedThreshold = 50;

        /// <summary>Concussion bar value that triggers a concussed proc.</summary>
        public const int ConcussedThreshold = 100;

        /// <summary>Concussion bar value retained after a concussed proc triggers (instead of resetting to 0).</summary>
        public const int ConcussedRetainedBar = 40;

        /// <summary>Concussion bar reduction per unit turn (decay).</summary>
        public const int DecayPerTurn = 20;

        /// <summary>
        /// Damage multiplier applied when the actor is dazed.
        /// dazedDamage = floor(rawDamage × DazedDamageMultiplier).
        /// </summary>
        public const double DazedDamageMultiplier = 0.8;

        // ── Status effect IDs ────────────────────────────────────────────────

        /// <summary>Status effect ID for Concussion bar ≥ 50.</summary>
        public const string StatusDazed = "dazed";

        /// <summary>Status effect ID while a unit is concussed (1 turn lost).</summary>
        public const string StatusConcussed = "concussed";

        // ── Bar key ──────────────────────────────────────────────────────────

        /// <summary>Key used in the unified bar dictionary for the concussion buildup bar.</summary>
        public const string BarConcussion = "concussion";

        // ── Concussion application ───────────────────────────────────────────

        /// <summary>
        /// Applies concussion power to a unit's concussion bar.
        /// Power is reduced by the unit's <paramref name="physicalResistance"/> before being added
        /// (capped at 90 % so that even fully-resistant units still accumulate some buildup).
        /// </summary>
        /// <param name="power">Concussion power to apply (from the skill's BuildupPower field).</param>
        /// <param name="physicalResistance">
        /// Target's physical resistance percentage (0 = none, 50 = half, 100 capped at 90, negative = weakness).
        /// </param>
        /// <param name="currentBar">Current concussion bar value before this application.</param>
        /// <param name="newBar">Concussion bar value after this application (capped at <see cref="MaxBar"/>).</param>
        /// <returns>Amount of concussion actually built (after resistance).</returns>
        public static int ApplyConcussion(int power, int physicalResistance, int currentBar, out int newBar)
        {
            // Resistance capped at 90: even full physical immunity still allows ≥10% buildup through.
            double factor = Math.Max(0.0, 1.0 - Math.Min(90, physicalResistance) / 100.0);
            int built = (int)(power * factor);
            newBar = Math.Min(MaxBar, currentBar + built);
            return built;
        }

        // ── Threshold check ──────────────────────────────────────────────────

        /// <summary>
        /// Checks if a concussed proc should trigger (concussion bar at <see cref="ConcussedThreshold"/>).
        /// If triggered, reduces <paramref name="bar"/> to <see cref="ConcussedRetainedBar"/>.
        /// </summary>
        /// <param name="bar">Concussion bar value, modified in-place if the proc triggers.</param>
        /// <returns>True when a concussed proc was triggered.</returns>
        public static bool CheckConcussedTriggered(ref int bar)
        {
            if (bar >= ConcussedThreshold)
            {
                bar = ConcussedRetainedBar;
                return true;
            }
            return false;
        }

        // ── Concussion decay ─────────────────────────────────────────────────

        /// <summary>
        /// Reduces the concussion bar by its per-turn decay constant, floored at 0.
        /// </summary>
        public static int ApplyDecay(int bar) => Math.Max(0, bar - DecayPerTurn);

        // ── Status effect derivation ─────────────────────────────────────────

        /// <summary>
        /// Returns the list of active concussion status effect IDs derived from the given bar value.
        /// <para>
        /// concussed and dazed are mutually exclusive in the status list (concussed takes priority),
        /// because a concussed unit cannot act and dazed only matters when acting.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> GetConcussionStatusEffects(int bar, bool isConcussed)
        {
            var effects = new List<string>();
            if (isConcussed)
                effects.Add(StatusConcussed);
            else if (bar >= DazedThreshold)
                effects.Add(StatusDazed);
            return effects;
        }

        // ── Dazed damage reduction ───────────────────────────────────────────

        /// <summary>
        /// Applies the dazed damage penalty when the actor is dazed.
        /// Dazed reduces final damage dealt by 20 %: <c>floor(damage × DazedDamageMultiplier)</c>.
        /// </summary>
        public static int ApplyDazedReduction(int damage, bool isDazed) =>
            isDazed ? (int)(damage * DazedDamageMultiplier) : damage;
    }
}
