namespace GameCore.Content.Raw;

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
    public string? SetTarget { get; set; }
    public string? SetKind { get; set; }
    public int? SetCooldown { get; set; }
    public int? SetInitialCooldown { get; set; }
    public string? SetEffectType { get; set; }
}
