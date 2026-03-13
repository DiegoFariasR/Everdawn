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

        // ── Penetration stats ─────────────────────────────────────────────────

        /// <summary>Physical resistance penetration percentage. Reduces the target's effective physical resistance.</summary>
        PhysicalPenetration,

        /// <summary>Fire resistance penetration percentage.</summary>
        FirePenetration,

        /// <summary>Cold resistance penetration percentage.</summary>
        ColdPenetration,

        /// <summary>Lightning resistance penetration percentage.</summary>
        LightningPenetration,

        /// <summary>Holy resistance penetration percentage.</summary>
        HolyPenetration,

        /// <summary>Void resistance penetration percentage.</summary>
        VoidPenetration,
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
