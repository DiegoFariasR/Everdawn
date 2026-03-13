using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// A compiled skill modifier, loaded from content data.
    /// Modifiers apply three ordered action groups to skill variables:
    ///   1. Set    — override/replace variable values (last modifier with a key wins).
    ///   2. Modify — additive numeric delta applied after Set (all deltas for a key are summed).
    ///   3. Add    — append typed damage components to the first effect's DamagePerHit.
    /// Supported Set/Modify variable keys: cost, damageMultiplier, isAoe, cooldown, initialCooldown.
    /// </summary>
    public record BattleModifier(
        string Id,
        string Name,
        string Description,
        /// <summary>Override variable values. Applied first; last modifier with a given key wins.</summary>
        IReadOnlyDictionary<string, object>? Set = null,
        /// <summary>Additive numeric deltas. Applied after Set. All deltas for a key are summed.</summary>
        IReadOnlyDictionary<string, double>? Modify = null,
        /// <summary>
        /// Damage components appended to the first effect's DamagePerHit after Set and Modify.
        /// Null when none. Only affects Attack skills, not Spells.
        /// </summary>
        IReadOnlyList<DamageComponent>? AddDamagePerHit = null
    );
}
