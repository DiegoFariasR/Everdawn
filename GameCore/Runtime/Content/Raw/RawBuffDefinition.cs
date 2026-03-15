namespace GameCore.Content.Raw
{
    /// <summary>
    /// Raw deserialized form of a reusable buff definition from <c>buff-definitions.yml</c>.
    /// Compiled into <see cref="Battle.ActiveEffectDefinition"/> by the content pipeline.
    /// </summary>
    public class RawBuffDefinition
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public int Duration { get; set; }
        public string DurationKind { get; set; } = "ForTargetTurns";
        public string StackingPolicy { get; set; } = "RefreshDuration";
        public RawEffectStats Stats { get; set; } = new RawEffectStats();
        /// <summary>
        /// When true, this effect is classified as a debuff (harmful).
        /// When false (default), it is a buff (beneficial).
        /// Determines eligibility as a target for dispel skills.
        /// </summary>
        public bool IsDebuff { get; set; } = false;
        /// <summary>
        /// Optional authoring description used to generate an SVG icon for this buff/debuff.
        /// Not used at runtime. Format: one short sentence describing the intended visual.
        /// </summary>
        public string? IconDescription { get; set; }
    }
}
