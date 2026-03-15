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

        /// <summary>
        /// Disruption bar gain resistance percentage.
        /// 0 = none, 50 = half buildup, 100 = immune, negative = weakness.
        /// </summary>
        DisruptionResistance,

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
