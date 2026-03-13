using System;
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Central helper for the disruption buildup bar system.
    /// <para>
    /// Disruption represents physical impact (blunt/slam) and lightning nervous-system shock.
    /// Both feed the same single bar. Disruption is independent from the thermal system.
    /// </para>
    /// <para>
    /// Thresholds:
    /// <list type="bullet">
    ///   <item>Disruption ≥ 50 → <c>dizzy</c> (actor's final damage output reduced by 20 %)</item>
    ///   <item>Disruption = 100 → <c>stunned</c> for 1 turn, bar retained at <see cref="StunRetainedBar"/></item>
    /// </list>
    /// </para>
    /// <para>
    /// Buildup comes from <see cref="SkillEffect.DisruptionPower"/> declared on each skill effect,
    /// not from a global blanket rule. Lightning/blunt skills that should build disruption must
    /// carry an explicit <c>DisruptionPower</c> value.
    /// </para>
    /// </summary>
    public static class DisruptionSystem
    {
        // ── Tuning constants ─────────────────────────────────────────────────

        /// <summary>Maximum value for the disruption bar.</summary>
        public const int MaxBar = 100;

        /// <summary>Disruption bar value at which the <c>dizzy</c> debuff is applied.</summary>
        public const int DizzyThreshold = 50;

        /// <summary>Disruption bar value that triggers a stun proc.</summary>
        public const int StunThreshold = 100;

        /// <summary>Disruption bar value retained after a stun triggers (instead of resetting to 0).</summary>
        public const int StunRetainedBar = 40;

        /// <summary>Disruption bar reduction per unit turn (decay).</summary>
        public const int DecayPerTurn = 20;

        /// <summary>
        /// Damage multiplier applied when the actor is dizzy.
        /// dizzyDamage = floor(rawDamage × DizzyDamageMultiplier).
        /// </summary>
        public const double DizzyDamageMultiplier = 0.8;

        // ── Status effect IDs ────────────────────────────────────────────────

        /// <summary>Status effect ID for Disruption bar ≥ 50.</summary>
        public const string StatusDizzy = "dizzy";

        /// <summary>Status effect ID while a unit is stunned (1 turn lost).</summary>
        public const string StatusStunned = "stunned";

        // ── Bar key ──────────────────────────────────────────────────────────

        /// <summary>Key used in the unified bar dictionary for the disruption buildup bar.</summary>
        public const string BarDisruption = "disruption";

        // ── Disruption application ───────────────────────────────────────────

        /// <summary>
        /// Applies disruption power to a unit's disruption bar.
        /// Power is reduced by the unit's <paramref name="disruptionResistance"/> before being added.
        /// </summary>
        /// <param name="power">Disruption power to apply (from the skill's DisruptionPower field).</param>
        /// <param name="disruptionResistance">
        /// Target's disruption resistance percentage (0 = none, 50 = half, 100 = immune, negative = weakness).
        /// </param>
        /// <param name="currentBar">Current disruption bar value before this application.</param>
        /// <param name="newBar">Disruption bar value after this application (capped at <see cref="MaxBar"/>).</param>
        /// <returns>Amount of disruption actually built (after resistance).</returns>
        public static int ApplyDisruption(int power, int disruptionResistance, int currentBar, out int newBar)
        {
            // Resistance capped at 90: even full immunity still allows ≥10% disruption through.
            double factor = Math.Max(0.0, 1.0 - Math.Min(90, disruptionResistance) / 100.0);
            int built = (int)(power * factor);
            newBar = Math.Min(MaxBar, currentBar + built);
            return built;
        }

        // ── Threshold check ──────────────────────────────────────────────────

        /// <summary>
        /// Checks if a stun should trigger (disruption bar at <see cref="StunThreshold"/>).
        /// If triggered, reduces <paramref name="bar"/> to <see cref="StunRetainedBar"/>.
        /// </summary>
        /// <param name="bar">Disruption bar value, modified in-place if stun triggers.</param>
        /// <returns>True when a stun was triggered.</returns>
        public static bool CheckStunTriggered(ref int bar)
        {
            if (bar >= StunThreshold)
            {
                bar = StunRetainedBar;
                return true;
            }
            return false;
        }

        // ── Disruption decay ─────────────────────────────────────────────────

        /// <summary>
        /// Reduces the disruption bar by its per-turn decay constant, floored at 0.
        /// </summary>
        public static int ApplyDecay(int bar) => Math.Max(0, bar - DecayPerTurn);

        // ── Status effect derivation ─────────────────────────────────────────

        /// <summary>
        /// Returns the list of active disruption status effect IDs derived from the given bar value.
        /// <para>
        /// stunned and dizzy are mutually exclusive in the status list (stunned takes priority),
        /// because a stunned unit cannot act and dizzy only matters when acting.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> GetDisruptionStatusEffects(int bar, bool isStunned)
        {
            var effects = new List<string>();
            if (isStunned)
                effects.Add(StatusStunned);
            else if (bar >= DizzyThreshold)
                effects.Add(StatusDizzy);
            return effects;
        }

        // ── Dizzy damage reduction ───────────────────────────────────────────

        /// <summary>
        /// Applies the dizzy damage penalty when the actor is dizzy.
        /// Dizzy reduces final damage dealt by 20 %: <c>floor(damage × DizzyDamageMultiplier)</c>.
        /// </summary>
        public static int ApplyDizzyReduction(int damage, bool isDizzy) =>
            isDizzy ? (int)(damage * DizzyDamageMultiplier) : damage;
    }
}
