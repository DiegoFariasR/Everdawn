using System;
using System.Collections.Generic;
// Cold and burn are opposing bars (0-100). Applying one element removes the opposing bar first.
// Thresholds: cold 50=slow, 100=frozen (1-turn skip); burn 50=burning (DOT each turn).
namespace GameCore.Battle
{
    public static class ThermalSystem
    {
        // ── Tuning constants ─────────────────────────────────────────────────

        public const int MaxBar = 100;
        public const int SlowThreshold = 50;
        public const int FrozenThreshold = 100;
        public const int FrozenRetainedBar = 40;
        public const int BurningThreshold = 50;
        public const double BurnDotPerBarPoint = 0.5; // burnDamage = floor(bar x 0.5); at 100 = 50 damage
        public const int ColdDecayPerTurn = 15;
        public const int BurnDecayPerTurn = 10;

        // ── Status effect IDs ────────────────────────────────────────────────

        public const string StatusChilled = "chilled";
        public const string StatusSlow = "slow";
        public const string StatusFrozen = "frozen";
        public const string StatusHeated = "heated";
        public const string StatusBurning = "burning";

        // ── Bar keys ─────────────────────────────────────────────────────────

        public const string BarCold = "cold";
        public const string BarBurn = "burn";

        // ── Thermal application ──────────────────────────────────────────────

        // Removes burn bar first (ignoring resistance); leftover builds cold bar, reduced by resistance.
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

        // Removes cold bar first (ignoring resistance); leftover builds burn bar, reduced by resistance.
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

        // If coldBar ≥ 100, resets to FrozenRetainedBar and returns true.
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

        public static int ComputeBurnDot(int burnBar) =>
            (int)(burnBar * BurnDotPerBarPoint);

        // ── Thermal decay ─────────────────────────────────────────────────────

        public static (int newCold, int newBurn) ApplyDecay(int coldBar, int burnBar) =>
            (Math.Max(0, coldBar - ColdDecayPerTurn), Math.Max(0, burnBar - BurnDecayPerTurn));

        // ── Status effect derivation ─────────────────────────────────────────

        // Frozen and slow are mutually exclusive (frozen takes priority). Burning can co-exist with either.
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

        // AGI hits: 1 + agi/100; slow halves the base (floor(1/2)=0), min result 1.
        public static int ResolveAgiHits(int agi, bool isSlow)
        {
            int baseHits = 1;
            int agiBonusHits = agi / 100;
            int effectiveBase = isSlow ? baseHits / 2 : baseHits;
            return Math.Max(1, effectiveBase + agiBonusHits);
        }
    }
}
