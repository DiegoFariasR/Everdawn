namespace GameCore.Content.Raw;

/// <summary>Raw modifier data as parsed directly from YAML.</summary>
public class RawModifier
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // Nullable stat overrides — only fields explicitly set in YAML will override the base skill.
    public int? Cost { get; set; }
    public double? Multiplier { get; set; }
    public bool? IsAoe { get; set; }
    public string? Target { get; set; }
    public string? Kind { get; set; }
    public int? Cooldown { get; set; }
    public int? InitialCooldown { get; set; }
    public string? EffectType { get; set; }
}
