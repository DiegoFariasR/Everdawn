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
        var modifiers = LoadModifiers(Path.Combine(basePath, "modifiers.yml")).ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var skills = LoadSkills(Path.Combine(basePath, "skills")).ToDictionary(s => s.Id);
        var units = LoadUnits(Path.Combine(basePath, "units"), skills, modifiers);
        return new ContentDatabase(units, skills.Values, modifiers.Values);
    }

    // ── Modifiers ─────────────────────────────────────────────────────────

    private static IEnumerable<BattleModifier> LoadModifiers(string filePath)
    {
        if (!File.Exists(filePath))
            yield break;

        var list = ParseYaml<List<RawModifier>>(filePath);
        foreach (var raw in list)
            yield return new BattleModifier(
                raw.Id, raw.Name, raw.Description,
                SetCost: raw.SetCost,
                SetDamageMultiplier: raw.SetDamageMultiplier,
                SetIsAoe: raw.SetIsAoe,
                SetTarget: raw.SetTarget != null ? Enum.Parse<BattleSkillTarget>(raw.SetTarget, ignoreCase: true) : null,
                SetKind: raw.SetKind != null ? Enum.Parse<EffectKind>(raw.SetKind, ignoreCase: true) : null,
                SetCooldown: raw.SetCooldown,
                SetInitialCooldown: raw.SetInitialCooldown,
                SetEffectType: raw.SetEffectType != null ? Enum.Parse<EffectType>(raw.SetEffectType, ignoreCase: true) : null);
    }

    // ── Skills ────────────────────────────────────────────────────────────

    private static IEnumerable<BattleSkill> LoadSkills(string skillsPath)
    {
        if (!Directory.Exists(skillsPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(skillsPath, "*.yml"))
        {
            var list = ParseYaml<List<RawSkill>>(file);
            foreach (var raw in list)
                yield return CompileSkill(raw);
        }
    }

    // ── Units ─────────────────────────────────────────────────────────────

    private static IEnumerable<BattleUnit> LoadUnits(
        string unitsPath, IReadOnlyDictionary<string, BattleSkill> skillDict, IReadOnlyDictionary<string, BattleModifier> modifierDict)
    {
        if (!Directory.Exists(unitsPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(unitsPath, "*.yml"))
        {
            var raw = ParseYaml<RawUnit>(file);
            yield return CompileUnit(raw, skillDict, modifierDict);
        }
    }

    private static BattleUnit CompileUnit(RawUnit raw, IReadOnlyDictionary<string, BattleSkill> skillDict, IReadOnlyDictionary<string, BattleModifier> modifierDict)
    {
        var traits = raw.Traits
            .Select(t => Enum.Parse<BattleTrait>(t, ignoreCase: true))
            .ToArray();

        var skills = raw.Skills
            .Select(slot =>
            {
                var sk = skillDict.TryGetValue(slot.Id, out var found)
                    ? found
                    : throw new KeyNotFoundException(
                        $"Unit '{raw.Id}' references unknown skill '{slot.Id}'.");

                if (slot.Modifiers == null)
                    return sk;

                var compiledMods = slot.Modifiers
                    .Select(modId => modifierDict.TryGetValue(modId, out var mod)
                        ? mod
                        : throw new KeyNotFoundException(
                            $"Unit '{raw.Id}' skill slot '{slot.Id}' references unknown modifier '{modId}'."))
                    .ToArray();

                // Apply modifier stat overrides in list order; last modifier wins per field.
                return sk with
                {
                    Modifiers = compiledMods.Select(m => m.Id).ToArray(),
                    Cost = compiledMods.LastOrDefault(m => m.SetCost != null)?.SetCost ?? sk.Cost,
                    DamageMultiplier = compiledMods.LastOrDefault(m => m.SetDamageMultiplier != null)?.SetDamageMultiplier ?? sk.DamageMultiplier,
                    IsAoe = compiledMods.LastOrDefault(m => m.SetIsAoe != null)?.SetIsAoe ?? sk.IsAoe,
                    Target = compiledMods.LastOrDefault(m => m.SetTarget != null)?.SetTarget ?? sk.Target,
                    Kind = compiledMods.LastOrDefault(m => m.SetKind != null)?.SetKind ?? sk.Kind,
                    Cooldown = compiledMods.LastOrDefault(m => m.SetCooldown != null)?.SetCooldown ?? sk.Cooldown,
                    InitialCooldown = compiledMods.LastOrDefault(m => m.SetInitialCooldown != null)?.SetInitialCooldown ?? sk.InitialCooldown,
                    EffectType = compiledMods.LastOrDefault(m => m.SetEffectType != null)?.SetEffectType ?? sk.EffectType,
                };
            })
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

        return new BattleSkill(
            raw.Id, raw.Name,
            Cost: raw.Cost,
            DamageMultiplier: raw.DamageMultiplier,
            IsAoe: raw.IsAoe,
            Target: target,
            Kind: kind,
            Cooldown: raw.Cooldown,
            InitialCooldown: raw.InitialCooldown,
            EffectType: effectType);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static T ParseYaml<T>(string filePath)
    {
        using var reader = new StreamReader(filePath);
        return _deserializer.Deserialize<T>(reader);
    }
}
