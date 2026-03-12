using System.Collections.Generic;
namespace GameCore.Content.Raw
{
    /// <summary>
    /// A unit's reference to a skill entry, optionally overriding per-slot properties
    /// (e.g. modifiers). Allows the same base skill to appear as different slot variants
    /// for the same or different units.
    /// </summary>
    public class RawUnitSkill
    {
        public string Id { get; set; } = "";

        /// <summary>
        /// Overrides the base skill's modifiers for this slot.
        /// Null = no override (base skill modifiers are used).
        /// Empty list = explicitly no modifiers.
        /// </summary>
        public List<string>? Modifiers { get; set; }
    }
}
