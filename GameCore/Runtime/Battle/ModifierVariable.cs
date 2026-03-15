namespace GameCore.Battle
{
    // Variables a modifier can override (Set) or adjust (Modify).
    // Skill variables apply in skill slot modifier lists.
    // Disruption/thermal variables apply in unit modifier lists.
    // Elemental resistance/penetration use typed EffectType dicts in BattleModifier.
    public enum ModifierVariable
    {
        // ── Skill variables ──────────────────────────────────────────────────

        Cost,
        DamageMultiplier,
        IsAoe,             // only valid for Set, not Modify
        Cooldown,
        InitialCooldown,
        ExtraHits,         // Set overrides BaseHits; Modify adds on top; fractional ok (0.5 = half-power hit)

        // ── Disruption variables ─────────────────────────────────────────────

        DisruptionResistance,   // bar mechanic — separate from elemental resistance
        DisruptionPenetration,  // bar mechanic — reduces target's effective disruption resistance

        // ── Thermal protection ───────────────────────────────────────────────

        ThermalProtection,  // boosts fire/cold resistance for buildup only; does not affect damage reduction

        // ── Receiving multipliers ────────────────────────────────────────────

        ReceivingHealingMultiplier,
        ReceivingBarrierMultiplier,
    }
}
