using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore.Battle;
using GameCore.Content.Raw;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GameCore.Content
{
    /// <summary>
    /// Reads YAML files via an <see cref="IContentSource"/>, compiles them into domain objects,
    /// and returns a ready-to-use <see cref="ContentDatabase"/>.
    /// <para>
    /// Pipeline steps (Reader → Raw → Compile → Database):
    /// <list type="number">
    ///   <item>Discover all .yml files under <c>units/</c> and <c>skills/</c> via the source</item>
    ///   <item>Parse each file into raw objects (no logic)</item>
    ///   <item>Compile raw objects into domain types</item>
    ///   <item>Return a <see cref="ContentDatabase"/> keyed by unit ID</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Architecture note:</b> The pipeline never discovers paths on its own. Callers must
    /// provide an <see cref="IContentSource"/> that already points at the right content root.
    /// </para>
    /// </summary>
    public static class ContentPipeline
    {
        private static readonly IDeserializer _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// Loads and compiles all content accessed through <paramref name="source"/>.
        /// </summary>
        public static ContentDatabase Load(IContentSource source)
        {
            var modifiers = LoadModifiers(source).ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
            var skills = LoadSkills(source).ToDictionary(s => s.Id);
            var units = LoadUnits(source, skills, modifiers);
            return new ContentDatabase(units, skills.Values, modifiers.Values);
        }

        /// <summary>
        /// Convenience overload: wraps <paramref name="basePath"/> in a
        /// <see cref="FileSystemContentSource"/> and calls <see cref="Load(IContentSource)"/>.
        /// </summary>
        /// <param name="basePath">Absolute path to the <c>GameData/Base</c> directory.</param>
        public static ContentDatabase Load(string basePath) =>
            Load(new FileSystemContentSource(basePath));

        // ── Modifiers ─────────────────────────────────────────────────────────

        private static IEnumerable<BattleModifier> LoadModifiers(IContentSource source)
        {
            const string path = "modifiers.yml";
            if (!source.FileExists(path))
                yield break;

            var list = ParseYaml<List<RawModifier>>(source.ReadAllText(path));
            foreach (var raw in list)
                yield return new BattleModifier(
                    raw.Id, raw.Name, raw.Description,
                    SetCost: raw.SetCost,
                    SetDamageMultiplier: raw.SetDamageMultiplier,
                    SetIsAoe: raw.SetIsAoe,
                    SetCooldown: raw.SetCooldown,
                    SetInitialCooldown: raw.SetInitialCooldown,
                    AddCost: raw.AddCost,
                    AddDamageMultiplier: raw.AddDamageMultiplier,
                    AddCooldown: raw.AddCooldown,
                    AddInitialCooldown: raw.AddInitialCooldown);
        }

        // ── Skills ────────────────────────────────────────────────────────────

        private static IEnumerable<BattleSkill> LoadSkills(IContentSource source)
        {
            const string dir = "skills";
            if (!source.DirectoryExists(dir))
                yield break;

            foreach (var file in source.ListFiles(dir, "*.yml"))
            {
                var list = ParseYaml<List<RawSkill>>(source.ReadAllText(file));
                foreach (var raw in list)
                    yield return CompileSkill(raw);
            }
        }

        // ── Units ─────────────────────────────────────────────────────────────

        private static IEnumerable<BattleUnit> LoadUnits(
            IContentSource source, IReadOnlyDictionary<string, BattleSkill> skillDict, IReadOnlyDictionary<string, BattleModifier> modifierDict)
        {
            const string dir = "units";
            if (!source.DirectoryExists(dir))
                yield break;

            foreach (var file in source.ListFiles(dir, "*.yml"))
            {
                var raw = ParseYaml<RawUnit>(source.ReadAllText(file));
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
                    // Set overrides are applied first, then Add deltas are summed on top.
                    var setCost = compiledMods.LastOrDefault(m => m.SetCost != null)?.SetCost ?? sk.Cost;
                    var setDmgMult = compiledMods.LastOrDefault(m => m.SetDamageMultiplier != null)?.SetDamageMultiplier ?? sk.DamageMultiplier;
                    var setCooldown = compiledMods.LastOrDefault(m => m.SetCooldown != null)?.SetCooldown ?? sk.Cooldown;
                    var setInitCd = compiledMods.LastOrDefault(m => m.SetInitialCooldown != null)?.SetInitialCooldown ?? sk.InitialCooldown;

                    return sk with
                    {
                        Modifiers = compiledMods.Select(m => m.Id).ToArray(),
                        Cost = setCost + compiledMods.Sum(m => m.AddCost ?? 0),
                        DamageMultiplier = setDmgMult + compiledMods.Sum(m => m.AddDamageMultiplier ?? 0.0),
                        IsAoe = compiledMods.LastOrDefault(m => m.SetIsAoe != null)?.SetIsAoe ?? sk.IsAoe,
                        Cooldown = setCooldown + compiledMods.Sum(m => m.AddCooldown ?? 0),
                        InitialCooldown = setInitCd + compiledMods.Sum(m => m.AddInitialCooldown ?? 0),
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
            var effects = new List<SkillEffect>();
            foreach (var rawEffect in raw.Effects)
            {
                var kind = Enum.Parse<EffectKind>(rawEffect.Kind, ignoreCase: true);
                var target = Enum.Parse<BattleSkillTarget>(rawEffect.Target, ignoreCase: true);
                var components = new List<DamageComponent>();
                foreach (var rawComp in rawEffect.DamagePerHit)
                {
                    EffectType? damageType = rawComp.DamageType != null
                        ? Enum.Parse<EffectType>(rawComp.DamageType, ignoreCase: true)
                        : null;
                    var scaling = rawComp.Scaling
                        .Select(s => new DamageScaling(s.Stat, s.Scale))
                        .ToArray();
                    components.Add(new DamageComponent(damageType, scaling));
                }
                effects.Add(new SkillEffect(kind, target, components));
            }

            return new BattleSkill(
                raw.Id, raw.Name,
                Cost: raw.Cost,
                DamageMultiplier: raw.DamageMultiplier,
                Effects: effects,
                IsAoe: raw.IsAoe,
                Cooldown: raw.Cooldown,
                InitialCooldown: raw.InitialCooldown,
                NumberOfHits: raw.NumberOfHits,
                HitsScaling: raw.HitsScaling.Count == 0
                    ? null
                    : raw.HitsScaling.Select(s => new DamageScaling(s.Stat, s.Scale)).ToArray<DamageScaling>(),
                Range: Enum.Parse<SkillRange>(raw.Range, ignoreCase: true),
                Category: Enum.Parse<SkillCategory>(raw.Category, ignoreCase: true));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static T ParseYaml<T>(string yamlText)
        {
            using var reader = new StringReader(yamlText);
            return _deserializer.Deserialize<T>(reader);
        }
    }
}
