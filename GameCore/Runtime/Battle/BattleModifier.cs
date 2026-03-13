using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// A skill modifier definition, loaded from content data.
    /// Modifiers describe how a skill is used (e.g. as a basic action or ultimate).
    /// Any non-null stat field overrides the corresponding field on the base skill
    /// when the modifier is applied during content compilation.
    /// </summary>
    public record BattleModifier(
        string Id,
        string Name,
        string Description,
        int? SetCost = null,
        double? SetDamageMultiplier = null,
        bool? SetIsAoe = null,
        int? SetCooldown = null,
        int? SetInitialCooldown = null,
        // Additive adjustments — applied after all Set overrides. Can be negative.
        int? ModifyCost = null,
        double? ModifyDamageMultiplier = null,
        int? ModifyCooldown = null,
        int? ModifyInitialCooldown = null,
        /// <summary>
        /// Extra damage components appended to the first effect's DamagePerHit after all other overrides.
        /// Null when none. Used by Enchanting modifiers to add elemental (e.g. WIS-based) damage.
        /// </summary>
        IReadOnlyList<DamageComponent>? AddDamageComponents = null
    );
}
