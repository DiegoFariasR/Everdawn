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
    public List<string> Traits { get; set; } = new List<string>();
    public List<RawUnitSkill> Skills { get; set; } = new List<RawUnitSkill>();  // resolved by ContentPipeline
    public Dictionary<string, int> Resistances { get; set; } = new Dictionary<string, int>();
}
