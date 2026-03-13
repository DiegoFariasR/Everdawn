namespace GameCore.Battle
{
    /// <summary>
    /// The skill variables that a modifier can override (Set) or adjust (Modify).
    /// </summary>
    public enum ModifierVariable
    {
        /// <summary>MP cost of the skill.</summary>
        Cost,
        /// <summary>Multiplier applied to the entire skill's damage output.</summary>
        DamageMultiplier,
        /// <summary>Whether the skill hits all enemies (AoE). Only valid for Set, not Modify.</summary>
        IsAoe,
        /// <summary>Turns the unit must wait before using the skill again.</summary>
        Cooldown,
        /// <summary>Cooldown the skill starts with at the beginning of battle.</summary>
        InitialCooldown,
    }
}
