#nullable enable
using System;
namespace GameCore.Battle
{
    /// <summary>
    /// Authoritative source for the Fury resource system.
    /// <para>
    /// Fury is a STR-oriented combat state that rises when a unit takes damage or uses STR-tagged
    /// skills, and decays at the end of each turn. High Fury boosts the power of STR-tagged skills
    /// dynamically — the same skill performs better the higher the actor's Fury is.
    /// </para>
    /// <para>
    /// Design pillars:
    /// <list type="bullet">
    ///   <item>Shirtless / low-armour barbarians gain more Fury because they lose more HP.</item>
    ///   <item>Heavy-armoured STR users still gain some Fury from the flat on-hit gain.</item>
    ///   <item>AGI and multihit builds do not become top Fury generators.</item>
    ///   <item>Full Fury means peak state — payoff is defined by the unit's passive/profile.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class FurySystem
    {
        /// <summary>Bar key used in <see cref="BattleUnit.MaxBars"/> and the session's bar store.</summary>
        public const string BarFury = "fury";

        /// <summary>Maximum Fury value.</summary>
        public const int MaxBar = 100;

        // ── Gain constants ────────────────────────────────────────────────────

        /// <summary>
        /// Flat Fury gained whenever the unit takes a direct damage hit that reduces HP
        /// (after barrier absorption; only fires when hpActuallyLost > 0).
        /// Represents the basic momentum gained from being struck, regardless of armour.
        /// </summary>
        public const int FlatGainOnHit = 8;

        /// <summary>
        /// Additional Fury gained per 1% of max HP lost from a single hit (after barrier).
        /// At 100% HP lost: +40 Fury on top of <see cref="FlatGainOnHit"/>.
        /// Represents the raw brutality that fuels barbarian-style Fury generation.
        /// </summary>
        public const double HpPctGainPerPoint = 0.4;

        /// <summary>
        /// Fury gained once when the actor uses a STR-tagged skill.
        /// Granted once per skill execution regardless of hit count or target count.
        /// </summary>
        public const int SkillUseGain = 15;

        // ── Decay constants ───────────────────────────────────────────────────

        /// <summary>
        /// Flat Fury lost at the start of a unit's turn (equivalent to end-of-previous-turn decay).
        /// Unconditional — applies whether the unit attacks, supports, or uses Focus.
        /// </summary>
        public const int DecayPerTurn = 15;

        // ── Gain helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Computes total Fury gained from a single incoming hit.
        /// <para>Formula: <see cref="FlatGainOnHit"/> + floor(hpLostPct × <see cref="HpPctGainPerPoint"/>).</para>
        /// </summary>
        /// <param name="hpLost">Actual HP subtracted from the unit this hit (after barrier absorbs damage).</param>
        /// <param name="maxHp">Unit's maximum HP.</param>
        /// <returns>Fury to add, always positive.</returns>
        public static int ComputeHitGain(int hpLost, int maxHp)
        {
            if (maxHp <= 0) return FlatGainOnHit;
            double pctLost = (double)hpLost / maxHp * 100.0;
            int bonus = (int)(pctLost * HpPctGainPerPoint);
            return FlatGainOnHit + bonus;
        }

        // ── Decay helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Applies per-turn decay to the given Fury value. Result is clamped to 0.
        /// </summary>
        public static int ApplyDecay(int currentFury) =>
            Math.Max(0, currentFury - DecayPerTurn);

        // ── Skill bonus helpers ───────────────────────────────────────────────

        /// <summary>
        /// Computes the outgoing damage multiplier bonus granted by the actor's current Fury
        /// to a STR-tagged skill.
        /// <para>Formula: 1.0 + <paramref name="furyDamageScale"/> × (fury / <see cref="MaxBar"/>).</para>
        /// </summary>
        /// <param name="fury">Current Fury value (0–100).</param>
        /// <param name="furyDamageScale">
        /// Maximum bonus at full Fury, from <see cref="BattleSkill.FuryDamageScale"/>.
        /// 0.5 means up to +50% damage at max Fury.
        /// </param>
        /// <returns>Multiplier ≥ 1.0.</returns>
        public static double ComputeDamageBonus(int fury, double furyDamageScale) =>
            1.0 + furyDamageScale * (fury / (double)MaxBar);
    }
}
