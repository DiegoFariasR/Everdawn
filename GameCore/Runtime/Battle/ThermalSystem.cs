using System;
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// Central helper for the opposed thermal (cold / burn) status system.
    /// <para>
    /// Cold and burn are opposing buildup bars (0–100 each).
    /// Applying one element first removes the opposing bar (ignoring resistance),
    /// and only the leftover power builds the matching bar, reduced by the unit's resistance.
    /// </para>
    /// <para>
    /// Thresholds:
    /// <list type="bullet">
    ///   <item>Cold ≥ 50 → <c>slow</c></item>
    ///   <item>Cold = 100 → <c>frozen</c> for 1 turn, bar retained at <see cref="FrozenRetainedBar"/></item>
    ///   <item>Burn ≥ 50 → <c>burning</c> (DOT scales linearly with current bar)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ThermalSystem
    {
        // ── Tuning constants ─────────────────────────────────────────────────

        /// <summary>Maximum value for the cold or burn bar.</summary>
        public const int MaxBar = 100;

        /// <summary>Cold bar value at which the <c>slow</c> debuff is applied.</summary>
        public const int SlowThreshold = 50;

        /// <summary>Cold bar value that triggers a freeze proc.</summary>
        public const int FrozenThreshold = 100;

        /// <summary>Cold bar value retained after a freeze triggers (instead of resetting to 0).</summary>
        public const int FrozenRetainedBar = 40;

        /// <summary>Burn bar value at which the <c>burning</c> debuff begins.</summary>
        public const int BurningThreshold = 50;

        /// <summary>
        /// Burn DOT coefficient: <c>burnDamage = floor(burnBar × BurnDotPerBarPoint)</c>.
        /// At Burn = 50 → 25 damage. At Burn = 100 → 50 damage.
        /// </summary>
        public const double BurnDotPerBarPoint = 0.5;

        /// <summary>Cold bar reduction per unit turn (decay).</summary>
        public const int ColdDecayPerTurn = 15;

        /// <summary>Burn bar reduction per unit turn (decay).</summary>
        public const int BurnDecayPerTurn = 10;

        // ── Status effect IDs ────────────────────────────────────────────────

        /// <summary>Status effect ID for Cold bar &gt; 0 and &lt; 50 (precursor warning).</summary>
        public const string StatusChilled = "chilled";

        /// <summary>Status effect ID for Cold bar ≥ 50.</summary>
        public const string StatusSlow = "slow";

        /// <summary>Status effect ID while a unit is frozen (1 turn lost).</summary>
        public const string StatusFrozen = "frozen";

        /// <summary>Status effect ID for Burn bar &gt; 0 and &lt; 50 (precursor warning).</summary>
        public const string StatusHeated = "heated";

        /// <summary>Status effect ID for Burn bar ≥ 50.</summary>
        public const string StatusBurning = "burning";

        // ── Bar keys ─────────────────────────────────────────────────────────

        /// <summary>Key used in the unified bar dictionary for the cold buildup bar.</summary>
        public const string BarCold = "cold";

        /// <summary>Key used in the unified bar dictionary for the burn buildup bar.</summary>
        public const string BarBurn = "burn";

        // ── Thermal application ──────────────────────────────────────────────

        /// <summary>
        /// Applies cold power to a unit's thermal bars.
        /// Removes burn bar first (ignoring resistance); only leftover builds cold bar, reduced by cold resistance.
        /// </summary>
        /// <param name="coldPower">Total cold power to apply (typically the raw hit damage).</param>
        /// <param name="coldResistance">Target's cold resistance percentage (0 = none, 50 = half, 100 = immune, negative = weakness).</param>
        /// <param name="currentBurnBar">Current burn bar value before this application.</param>
        /// <param name="currentColdBar">Current cold bar value before this application.</param>
        /// <param name="newBurnBar">Burn bar value after this application.</param>
        /// <param name="newColdBar">Cold bar value after this application (capped at <see cref="MaxBar"/>).</param>
        /// <returns>(burnRemoved, coldBuilt) amounts for event descriptions.</returns>
        public static (int burnRemoved, int coldBuilt) ApplyCold(
            int coldPower,
            int coldResistance,
            int currentBurnBar,
            int currentColdBar,
            out int newBurnBar,
            out int newColdBar)
        {
            // Step 1: remove burn bar first, ignoring resistance
            int burnRemoved = Math.Min(currentBurnBar, coldPower);
            newBurnBar = currentBurnBar - burnRemoved;

            // Step 2: leftover cold builds cold bar, reduced by cold resistance
            int leftover = coldPower - burnRemoved;
            int coldBuilt = 0;
            if (leftover > 0)
            {
                // Resistance capped at 90: even full immunity still allows ≥10% cold through.
                double factor = Math.Max(0.0, 1.0 - Math.Min(90, coldResistance) / 100.0);
                coldBuilt = (int)(leftover * factor);
                newColdBar = Math.Min(MaxBar, currentColdBar + coldBuilt);
            }
            else
            {
                newColdBar = currentColdBar;
            }

            return (burnRemoved, coldBuilt);
        }

        /// <summary>
        /// Applies fire power to a unit's thermal bars.
        /// Removes cold bar first (ignoring resistance); only leftover builds burn bar, reduced by fire resistance.
        /// </summary>
        /// <param name="firePower">Total fire power to apply (typically the raw hit damage).</param>
        /// <param name="fireResistance">Target's fire resistance percentage.</param>
        /// <param name="currentColdBar">Current cold bar value before this application.</param>
        /// <param name="currentBurnBar">Current burn bar value before this application.</param>
        /// <param name="newColdBar">Cold bar value after this application.</param>
        /// <param name="newBurnBar">Burn bar value after this application (capped at <see cref="MaxBar"/>).</param>
        /// <returns>(coldRemoved, burnBuilt) amounts for event descriptions.</returns>
        public static (int coldRemoved, int burnBuilt) ApplyFire(
            int firePower,
            int fireResistance,
            int currentColdBar,
            int currentBurnBar,
            out int newColdBar,
            out int newBurnBar)
        {
            // Step 1: remove cold bar first, ignoring resistance
            int coldRemoved = Math.Min(currentColdBar, firePower);
            newColdBar = currentColdBar - coldRemoved;

            // Step 2: leftover fire builds burn bar, reduced by fire resistance
            int leftover = firePower - coldRemoved;
            int burnBuilt = 0;
            if (leftover > 0)
            {
                // Resistance capped at 90: even full immunity still allows ≥10% fire through.
                double factor = Math.Max(0.0, 1.0 - Math.Min(90, fireResistance) / 100.0);
                burnBuilt = (int)(leftover * factor);
                newBurnBar = Math.Min(MaxBar, currentBurnBar + burnBuilt);
            }
            else
            {
                newBurnBar = currentBurnBar;
            }

            return (coldRemoved, burnBuilt);
        }

        // ── Threshold check ──────────────────────────────────────────────────

        /// <summary>
        /// Checks if a freeze should trigger (cold bar at <see cref="FrozenThreshold"/>).
        /// If triggered, reduces <paramref name="coldBar"/> to <see cref="FrozenRetainedBar"/>.
        /// </summary>
        /// <param name="coldBar">Cold bar value, modified in-place if freeze triggers.</param>
        /// <returns>True when a freeze was triggered.</returns>
        public static bool CheckFreezeTriggered(ref int coldBar)
        {
            if (coldBar >= FrozenThreshold)
            {
                coldBar = FrozenRetainedBar;
                return true;
            }
            return false;
        }

        // ── Burn DOT ─────────────────────────────────────────────────────────

        /// <summary>
        /// Computes burn damage-over-time for the current burn bar value.
        /// <c>burnDamage = floor(burnBar × <see cref="BurnDotPerBarPoint"/>)</c>
        /// </summary>
        public static int ComputeBurnDot(int burnBar) =>
            (int)(burnBar * BurnDotPerBarPoint);

        // ── Thermal decay ─────────────────────────────────────────────────────

        /// <summary>
        /// Reduces both bars by their per-turn decay constants.
        /// Returns the new (cold, burn) values, each floored at 0.
        /// </summary>
        public static (int newCold, int newBurn) ApplyDecay(int coldBar, int burnBar) =>
            (Math.Max(0, coldBar - ColdDecayPerTurn), Math.Max(0, burnBar - BurnDecayPerTurn));

        // ── Status effect derivation ─────────────────────────────────────────

        /// <summary>
        /// Returns the list of active thermal status effect IDs derived from the given bar values.
        /// This is the authoritative source Unity and the sandbox read for thermal status icons.
        /// <para>
        /// frozen and slow are mutually exclusive (frozen takes priority).
        /// burning can co-exist with either.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> GetThermalStatusEffects(
            int coldBar, int burnBar, bool isFrozen)
        {
            var effects = new List<string>();
            if (isFrozen)
                effects.Add(StatusFrozen);
            else if (coldBar >= SlowThreshold)
                effects.Add(StatusSlow);
            if (burnBar >= BurningThreshold)
                effects.Add(StatusBurning);
            return effects;
        }

        // ── Slow-aware hit resolution ────────────────────────────────────────

        /// <summary>
        /// Resolves the effective AGI-derived hit count for an actor, applying slow when active.
        /// <para>
        /// Base hit contribution = 1. AGI bonus = Agi / 100 (integer division).
        /// Slow halves the base contribution (integer division: 1/2 = 0).
        /// Result is always at least 1 — slow cannot reduce a unit to zero hits.
        /// </para>
        /// </summary>
        public static int ResolveAgiHits(int agi, bool isSlow)
        {
            int baseHits = 1;
            int agiBonusHits = agi / 100;
            int effectiveBase = isSlow ? baseHits / 2 : baseHits;
            return Math.Max(1, effectiveBase + agiBonusHits);
        }
    }
}
