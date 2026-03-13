namespace GameCore.Battle
{
    /// <summary>
    /// Controls how a second application of the same active effect definition is handled
    /// when an instance from that definition is already active on the target.
    /// </summary>
    public enum EffectStackingPolicy
    {
        /// <summary>
        /// Reset the remaining duration to the new application's duration.
        /// Modifiers do not change. Default for most buffs and debuffs.
        /// </summary>
        RefreshDuration,

        /// <summary>
        /// Replace the existing instance only if the new application has a longer remaining duration.
        /// Shorter re-applications are ignored.
        /// </summary>
        ReplaceIfStronger,

        /// <summary>
        /// Increment the stack count on the existing instance and extend duration to the
        /// maximum of the two. Modifier resolution uses the Stacks count as a display value;
        /// each stack adds an independent layer of modifiers.
        /// </summary>
        StackIntensity,

        /// <summary>
        /// Always create a new independent instance regardless of existing instances
        /// from the same definition. Allows unlimited separate applications.
        /// </summary>
        IndependentInstances,
    }
}
