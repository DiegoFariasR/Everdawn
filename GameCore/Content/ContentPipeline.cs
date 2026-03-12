using GameCore.Battle;
using GameCore.Content.Raw;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GameCore.Content;

/// <summary>
/// Reads YAML files from <c>GameData/Base/</c>, compiles them into domain objects,
/// and returns a ready-to-use <see cref="ContentDatabase"/>.
/// <para>
/// Pipeline steps (Reader → Raw → Compile → Database):
/// <list type="number">
///   <item>Discover all .yml files under <c>basePath/units/</c></item>
///   <item>Parse each file into a <see cref="RawUnit"/> (no logic)</item>
///   <item>Compile each <see cref="RawUnit"/> into a <see cref="BattleUnit"/></item>
///   <item>Return a <see cref="ContentDatabase"/> keyed by unit ID</item>
/// </list>
/// </para>
/// </summary>
public static class ContentPipeline
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Loads and compiles all content from <paramref name="basePath"/>.
    /// </summary>
    /// <param name="basePath">Path to the <c>GameData/Base</c> directory.</param>
    public static ContentDatabase Load(string basePath)
    {
        var units = LoadUnits(Path.Combine(basePath, "units"));
        return new ContentDatabase(units);
    }

    // ── Units ─────────────────────────────────────────────────────────────

    private static IEnumerable<BattleUnit> LoadUnits(string unitsPath)
    {
        if (!Directory.Exists(unitsPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(unitsPath, "*.yml"))
        {
            var raw = ParseYaml<RawUnit>(file);
            yield return CompileUnit(raw);
        }
    }

    private static BattleUnit CompileUnit(RawUnit raw)
    {
        var traits = raw.Traits
            .Select(t => Enum.Parse<BattleTrait>(t, ignoreCase: true))
            .ToArray();

        var skills = raw.Skills
            .Select(CompileSkill)
            .ToArray();

        IReadOnlyDictionary<EffectType, int>? resistances = null;
        if (raw.Resistances.Count > 0)
        {
            resistances = raw.Resistances.ToDictionary(
                kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true),
                kvp => kvp.Value);
        }

        return new BattleUnit(
            raw.Id, raw.Name,
            Team: "",           // team is assigned by the scenario, not the data file
            Level: raw.Level,
            Str: raw.Str,
            Wis: raw.Wis,
            Agi: raw.Agi,
            Skills: skills,
            Traits: traits,
            Resistances: resistances);
    }

    private static BattleSkill CompileSkill(RawSkill raw)
    {
        var kind = Enum.Parse<EffectKind>(raw.Kind, ignoreCase: true);
        var effectType = Enum.Parse<EffectType>(raw.EffectType, ignoreCase: true);
        var target = Enum.Parse<BattleSkillTarget>(raw.Target, ignoreCase: true);
        var modifiers = raw.Modifiers.Count > 0
            ? raw.Modifiers.Select(m => Enum.Parse<SkillModifier>(m, ignoreCase: true)).ToArray()
            : null;

        return new BattleSkill(
            raw.Id, raw.Name,
            MpCost: raw.MpCost,
            Multiplier: raw.Multiplier,
            IsAoe: raw.IsAoe,
            Target: target,
            Kind: kind,
            Cooldown: raw.Cooldown,
            InitialCooldown: raw.InitialCooldown,
            EffectType: effectType,
            Modifiers: modifiers);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static T ParseYaml<T>(string filePath)
    {
        using var reader = new StreamReader(filePath);
        return _deserializer.Deserialize<T>(reader);
    }
}
