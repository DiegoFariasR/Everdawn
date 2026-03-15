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
    }
}
