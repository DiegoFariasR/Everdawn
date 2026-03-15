#nullable enable
using System;
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Governs the bleed buildup bar driven by <see cref="EffectType.Slash"/> damage.
    /// <para>
    /// Bleed complements the disruption and thermal CC systems. Unlike disruption (which
    /// uses a separate <c>DisruptionResistance</c> stat), bleed buildup resistance is derived
    /// from the target's effective Slash resistance (which itself stacks with Physical resistance).
    /// This means a unit that resists slashing damage will also accumulate bleed more slowly.
    /// </para>
    /// <para>Soft threshold (<see cref="BleedingThreshold"/>) at 50 applies the
    /// <c>bleeding</c> DOT status: <see cref="BleedDotPerBarPoint"/> × bar per actor turn.
    /// The bar decays by <see cref="DecayPerTurn"/> at the start of each actor turn.</para>
    /// </summary>
    public static class BleedSystem
    {
        // ── Tuning constants ─────────────────────────────────────────────────
        public const int MaxBar = 100;
        public const int BleedingThreshold = 50;
        public const int DecayPerTurn = 10;
        public const double BleedDotPerBarPoint = 0.5;

        // ── Status effect ID ─────────────────────────────────────────────────
        public const string StatusBleeding = "bleeding";

        // ── Bar key ──────────────────────────────────────────────────────────
        public const string BarBleed = "bleed";

        // ── Bleed application ────────────────────────────────────────────────
        /// <summary>
        /// Applies <paramref name="power"/> bleed buildup to <paramref name="currentBar"/>,
        /// reduced by <paramref name="slashResistance"/> (capped at 90%).
        /// Returns the amount actually built; <paramref name="newBar"/> is the resulting bar value.
        /// </summary>
        public static int ApplyBleed(int power, int slashResistance, int currentBar, out int newBar)
        {
            double factor = Math.Max(0.0, 1.0 - Math.Min(90, slashResistance) / 100.0);
            int built = (int)(power * factor);
            newBar = Math.Min(MaxBar, currentBar + built);
            return built;
        }

        // ── Bleed DOT ────────────────────────────────────────────────────────
        /// <summary>Returns the bleed DOT damage for a unit with the given bar value.</summary>
        public static int ComputeBleedDot(int bleedBar) => (int)(bleedBar * BleedDotPerBarPoint);

        // ── Bleed decay ──────────────────────────────────────────────────────
        public static int ApplyDecay(int bar) => Math.Max(0, bar - DecayPerTurn);

        // ── Status effect derivation ─────────────────────────────────────────
        public static IReadOnlyList<string> GetBleedStatusEffects(int bar)
        {
            var effects = new List<string>();
            if (bar >= BleedingThreshold) effects.Add(StatusBleeding);
            return effects;
        }
    }
}
