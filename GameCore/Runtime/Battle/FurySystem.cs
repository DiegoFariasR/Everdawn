#nullable enable
using System;
// Fury rises from damage taken and STR skill use; decays per turn; boosts STR skill damage dynamically.
namespace GameCore.Battle
{
    public static class FurySystem
    {
        public const string BarFury = "fury";
        public const int MaxBar = 100;

        // ── Gain constants ────────────────────────────────────────────────────

        public const int FlatGainOnHit = 8;          // flat Fury per HP-reducing hit (regardless of armour)
        public const double HpPctGainPerPoint = 0.4; // bonus Fury per 1% max HP lost; at 100% HP lost = +40
        public const int SkillUseGain = 15;

        // ── Decay constants ───────────────────────────────────────────────────

        public const int DecayPerTurn = 15;

        // ── Gain helpers ──────────────────────────────────────────────────────

        // FlatGainOnHit + floor(hpLostPct × HpPctGainPerPoint)
        public static int ComputeHitGain(int hpLost, int maxHp)
        {
            if (maxHp <= 0) return FlatGainOnHit;
            double pctLost = (double)hpLost / maxHp * 100.0;
            int bonus = (int)(pctLost * HpPctGainPerPoint);
            return FlatGainOnHit + bonus;
        }

        // ── Decay helpers ─────────────────────────────────────────────────────

        public static int ApplyDecay(int currentFury) =>
            Math.Max(0, currentFury - DecayPerTurn);

        // ── Skill bonus helpers ───────────────────────────────────────────────

        // 1.0 + furyDamageScale × (fury / MaxBar)
        public static double ComputeDamageBonus(int fury, double furyDamageScale) =>
            1.0 + furyDamageScale * (fury / (double)MaxBar);
    }
}
