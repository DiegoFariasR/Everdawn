namespace GameCore.Content.Raw
{
    /// <summary>
    /// Raw YAML representation of a modifier tag rule.
    /// Defines how many skills tagged with a given modifier tag each unit must have.
    /// Loaded from <c>modifier-tag-rules.yml</c> by the content pipeline.
    /// </summary>
    public class RawModifierTagRule
    {
        /// <summary>The modifier tag this rule applies to (e.g. "basic", "ultimate", "reaction").</summary>
        public string Tag { get; set; } = "";

        /// <summary>
        /// The exact number of skills a unit must have that carry this modifier tag.
        /// The content pipeline throws if the count does not match.
        /// </summary>
        public int RequiredPerUnit { get; set; }
    }
}
