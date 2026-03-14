namespace GameCore.Battle
{
    /// <summary>
    /// Identifies which unit stat a <see cref="RuntimeStatModifier"/> targets.
    /// </summary>
    public enum RuntimeStatKey
    {
        /// <summary>Multiplier applied to all outgoing damage dealt by this unit. Default base value: 1.0.</summary>
        DamageDealtMultiplier,

        /// <summary>Multiplier applied to all incoming damage taken by this unit. Default base value: 1.0.</summary>
        DamageTakenMultiplier,

        /// <summary>Multiplier applied to all healing received by this unit. Default base value: 1.0.</summary>
        ReceivingHealingMultiplier,

        /// <summary>Multiplier applied to all barrier received by this unit. Default base value: 1.0.</summary>
        ReceivingBarrierMultiplier,

        // ── Per-type damage dealt multipliers ─────────────────────────────────

        /// <summary>Multiplier applied to outgoing physical damage dealt by this unit. Default base value: 1.0.</summary>
        PhysicalDamageDealtMultiplier,

        /// <summary>Multiplier applied to outgoing fire damage dealt by this unit. Default base value: 1.0.</summary>
        FireDamageDealtMultiplier,

        /// <summary>Multiplier applied to outgoing cold damage dealt by this unit. Default base value: 1.0.</summary>
        ColdDamageDealtMultiplier,

        /// <summary>Multiplier applied to outgoing lightning damage dealt by this unit. Default base value: 1.0.</summary>
        LightningDamageDealtMultiplier,

        /// <summary>Multiplier applied to outgoing holy damage dealt by this unit. Default base value: 1.0.</summary>
        HolyDamageDealtMultiplier,

        /// <summary>Multiplier applied to outgoing void damage dealt by this unit. Default base value: 1.0.</summary>
        VoidDamageDealtMultiplier,

        /// <summary>Physical damage resistance percentage. 0 = none, 50 = half damage, 100 = immune, negative = weakness.</summary>
        PhysicalResistance,

        /// <summary>Fire damage resistance percentage.</summary>
        FireResistance,

        /// <summary>Cold damage resistance percentage. Also affects cold buildup rate.</summary>
        ColdResistance,

        /// <summary>Lightning damage resistance percentage.</summary>
        LightningResistance,

        /// <summary>Holy damage resistance percentage.</summary>
        HolyResistance,

        /// <summary>Void damage resistance percentage.</summary>
        VoidResistance,

        /// <summary>
        /// Disruption bar gain resistance percentage.
        /// 0 = none, 50 = half buildup, 100 = immune, negative = weakness.
        /// </summary>
        DisruptionResistance,

        // ── Penetration keys ─────────────────────────────────────────────────

        /// <summary>Physical resistance penetration. Reduces the target's effective physical resistance.</summary>
        PhysicalPenetration,

        /// <summary>Fire resistance penetration. Reduces the target's effective fire resistance.</summary>
        FirePenetration,

        /// <summary>Cold resistance penetration. Reduces the target's effective cold resistance.</summary>
        ColdPenetration,

        /// <summary>Lightning resistance penetration. Reduces the target's effective lightning resistance.</summary>
        LightningPenetration,

        /// <summary>Holy resistance penetration. Reduces the target's effective holy resistance.</summary>
        HolyPenetration,

        /// <summary>Void resistance penetration. Reduces the target's effective void resistance.</summary>
        VoidPenetration,

        /// <summary>Disruption resistance penetration. Reduces the target's effective disruption resistance.</summary>
        DisruptionPenetration,

        /// <summary>
        /// Bonus percentage applied to fire and cold resistances for thermal buildup absorption.
        /// 0 = no bonus. 10 = resistances act 10% stronger for buildup purposes.
        /// Does not affect fire/cold damage reduction — only buildup bar accumulation.
        /// </summary>
        ThermalProtection,
    }

    /// <summary>
    /// The arithmetic operation to use when resolving a <see cref="RuntimeStatModifier"/>.
    /// Applied in deterministic order: Set → Add → Multiply.
    /// </summary>
    public enum ModifierOperation
    {
        /// <summary>Override the current stat value. When multiple Set modifiers are active, the last one wins.</summary>
        Set,

        /// <summary>Add a flat delta to the value after all Set operations are applied.</summary>
        Add,

        /// <summary>Multiply the value after all Set and Add operations are applied.</summary>
        Multiply,
    }

    /// <summary>
    /// A single stat modification contributed by an active effect at runtime.
    /// Multiple instances are resolved in deterministic order: Set → Add → Multiply.
    /// </summary>
    public record RuntimeStatModifier(
        RuntimeStatKey StatKey,
        ModifierOperation Operation,
        double Value
    );
}
