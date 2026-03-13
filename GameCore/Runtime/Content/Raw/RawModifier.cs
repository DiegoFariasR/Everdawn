using System.Collections.Generic;
using GameCore.Battle;
namespace GameCore.Content.Raw
{
    /// <summary>
    /// Container for typed Add actions supported by a modifier.
    /// Currently supports appending damage components to the first effect's DamagePerHit list.
    /// </summary>
    public class RawModifierAdd
    {
        /// <summary>
        /// Damage components appended to the first effect's DamagePerHit list.
        /// Applied after all Set and Modify operations. Only affects Attack skills, not Spells.
        /// </summary>
        public List<RawDamageComponent> DamagePerHit { get; set; } = new List<RawDamageComponent>();
    }

    /// <summary>
    /// Raw modifier data as parsed directly from YAML.
    /// A modifier applies three ordered action groups to skill variables:
    ///   1. Set    — override/replace variable values (last modifier with a key wins).
    ///   2. Modify — additive numeric delta applied after Set (all deltas for a key are summed).
    ///   3. Add    — append typed damage components to the first effect's DamagePerHit.
    /// Supported Set/Modify variable keys: see <see cref="GameCore.Battle.ModifierVariable"/>.
    /// </summary>
    public class RawModifier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>
        /// Override variable values. Keys: <see cref="ModifierVariable"/> members.
        /// Applied first; last modifier with a given key wins.
        /// </summary>
        public Dictionary<ModifierVariable, object> Set { get; set; } = new Dictionary<ModifierVariable, object>();

        /// <summary>
        /// Additive numeric deltas applied after Set overrides. Can be negative.
        /// Keys: <see cref="ModifierVariable"/> members (except <see cref="ModifierVariable.IsAoe"/>).
        /// All deltas for the same key are summed across all modifiers.
        /// </summary>
        public Dictionary<ModifierVariable, double> Modify { get; set; } = new Dictionary<ModifierVariable, double>();

        /// <summary>
        /// Typed Add actions — appends damage components to the first effect's DamagePerHit
        /// after Set and Modify are applied.
        /// </summary>
        public RawModifierAdd Add { get; set; } = new RawModifierAdd();
    }
}
