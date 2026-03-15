using System;
using System.Collections.Generic;
// Disruption is a single buildup bar (blunt/lightning).
// Thresholds: 50 = dizzy (output ×0.8); 100 = stunned (1-turn skip, bar retained at StunRetainedBar).
// Buildup comes from SkillEffect.DisruptionPower; no blanket rule by damage type.
namespace GameCore.Battle
{
    public static class DisruptionSystem
    {
        // ── Tuning constants ─────────────────────────────────────────────────

        public const int MaxBar = 100;
        public const int DizzyThreshold = 50;
        public const int StunThreshold = 100;
        public const int StunRetainedBar = 40;
        public const int DecayPerTurn = 20;
        public const double DizzyDamageMultiplier = 0.8; // floor(rawDamage × 0.8) when dizzy

        // ── Status effect IDs ────────────────────────────────────────────────

        public const string StatusShaken = "shaken";
        public const string StatusDizzy = "dizzy";
        public const string StatusStunned = "stunned";

        // ── Bar key ──────────────────────────────────────────────────────────

        public const string BarDisruption = "disruption";

        // ── Disruption application ───────────────────────────────────────────

        // Power is reduced by disruptionResistance (0=none, 50=half, 100=immune, negative=weakness).
        public static int ApplyDisruption(int power, int disruptionResistance, int currentBar, out int newBar)
        {
            // Resistance capped at 90: even full immunity still allows ≥10% disruption through.
            double factor = Math.Max(0.0, 1.0 - Math.Min(90, disruptionResistance) / 100.0);
            int built = (int)(power * factor);
            newBar = Math.Min(MaxBar, currentBar + built);
            return built;
        }

        // ── Threshold check ──────────────────────────────────────────────────

        // If bar ≥ 100, resets to StunRetainedBar and returns true.
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

        public static int ApplyDecay(int bar) => Math.Max(0, bar - DecayPerTurn);

        // ── Status effect derivation ─────────────────────────────────────────

        // Stunned and dizzy are mutually exclusive (stunned takes priority; dizzy only matters when acting).
        public static IReadOnlyList<string> GetDisruptionStatusEffects(int bar, bool isStunned)
        {
            var effects = new List<string>();
            if (isStunned)
                effects.Add(StatusStunned);
            else if (bar >= DizzyThreshold)
                effects.Add(StatusDizzy);
            else if (bar > 0)
                effects.Add(StatusShaken);
            return effects;
        }

        // ── Dizzy damage reduction ───────────────────────────────────────────

        public static int ApplyDizzyReduction(int damage, bool isDizzy) =>
            isDizzy ? (int)(damage * DizzyDamageMultiplier) : damage;
    }
}
