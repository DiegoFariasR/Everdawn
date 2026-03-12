namespace GameCore.Content.Raw;

/// <summary>Raw unit data as parsed directly from YAML. No validation or compilation yet.</summary>
public class RawUnit
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Str { get; set; }
    public int Wis { get; set; }
    public int Agi { get; set; }
    public List<string> Traits { get; set; } = [];
    public List<string> Skills { get; set; } = [];  // skill IDs; resolved by ContentPipeline
    public Dictionary<string, int> Resistances { get; set; } = [];
}
