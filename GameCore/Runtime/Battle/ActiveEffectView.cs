namespace GameCore.Battle
{
    /// <summary>
    /// A display-safe view of one active effect instance, exposed through <see cref="UnitState.ActiveEffects"/>.
    /// Contains enough information for the UI to show buff/debuff icons, names, and turn timers.
    /// Internal modifier formulas remain authoritative inside GameCore and are not exposed here.
    /// </summary>
    public record ActiveEffectView(
        /// <summary>ID of the <see cref="ActiveEffectDefinition"/> (e.g. "attackUp", "guard").</summary>
        string DefinitionId,

        /// <summary>Human-readable name (e.g. "Attack Up", "Guard").</summary>
        string Name,

        /// <summary>Remaining turns until this effect expires.</summary>
        int RemainingDuration,

        /// <summary>Stack count. Greater than 1 when <see cref="EffectStackingPolicy.StackIntensity"/> was used.</summary>
        int Stacks
    );
}
