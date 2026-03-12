namespace GameCore.Content.Raw
{
    /// <summary>Raw modifier data as parsed directly from YAML.</summary>
    public class RawModifier
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        // Nullable stat overrides — only fields explicitly set in YAML will override the base skill.
        public int? SetCost { get; set; }
        public double? SetDamageMultiplier { get; set; }
        public bool? SetIsAoe { get; set; }
        public int? SetCooldown { get; set; }
        public int? SetInitialCooldown { get; set; }

        // Additive adjustments — applied after all Set overrides. Can be negative.
        public int? AddCost { get; set; }
        public double? AddDamageMultiplier { get; set; }
        public int? AddCooldown { get; set; }
        public int? AddInitialCooldown { get; set; }
    }
}
