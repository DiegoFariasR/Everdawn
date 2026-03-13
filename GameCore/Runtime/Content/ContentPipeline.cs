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
            {
                // ── Scalar Set (skill variables + disruption) ─────────────────
                var scalarSet = new Dictionary<ModifierVariable, object>();
                if (raw.Set.Cost != null) scalarSet[ModifierVariable.Cost] = raw.Set.Cost.Value;
                if (raw.Set.DamageMultiplier != null) scalarSet[ModifierVariable.DamageMultiplier] = raw.Set.DamageMultiplier.Value;
                if (raw.Set.IsAoe != null) scalarSet[ModifierVariable.IsAoe] = raw.Set.IsAoe.Value;
                if (raw.Set.Cooldown != null) scalarSet[ModifierVariable.Cooldown] = raw.Set.Cooldown.Value;
                if (raw.Set.InitialCooldown != null) scalarSet[ModifierVariable.InitialCooldown] = raw.Set.InitialCooldown.Value;
                if (raw.Set.DisruptionResistance != null) scalarSet[ModifierVariable.DisruptionResistance] = raw.Set.DisruptionResistance.Value;
                if (raw.Set.DisruptionPenetration != null) scalarSet[ModifierVariable.DisruptionPenetration] = raw.Set.DisruptionPenetration.Value;

                // ── Scalar Modify ────────────────────────────────────────────
                var scalarModify = new Dictionary<ModifierVariable, double>();
                if (raw.Modify.Cost.HasValue) scalarModify[ModifierVariable.Cost] = raw.Modify.Cost.Value;
                if (raw.Modify.DamageMultiplier.HasValue) scalarModify[ModifierVariable.DamageMultiplier] = raw.Modify.DamageMultiplier.Value;
                if (raw.Modify.Cooldown.HasValue) scalarModify[ModifierVariable.Cooldown] = raw.Modify.Cooldown.Value;
                if (raw.Modify.InitialCooldown.HasValue) scalarModify[ModifierVariable.InitialCooldown] = raw.Modify.InitialCooldown.Value;
                if (raw.Modify.DisruptionResistance.HasValue) scalarModify[ModifierVariable.DisruptionResistance] = raw.Modify.DisruptionResistance.Value;
                if (raw.Modify.DisruptionPenetration.HasValue) scalarModify[ModifierVariable.DisruptionPenetration] = raw.Modify.DisruptionPenetration.Value;

                // ── Typed resistance/penetration dictionaries ─────────────────
                Dictionary<EffectType, int>? setResistances = raw.Set.Resistance.Count > 0
                    ? raw.Set.Resistance.ToDictionary(
                        kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;
                Dictionary<EffectType, double>? modifyResistances = raw.Modify.Resistance.Count > 0
                    ? raw.Modify.Resistance.ToDictionary(
                        kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;
                Dictionary<EffectType, int>? setPenetrations = raw.Set.Penetration.Count > 0
                    ? raw.Set.Penetration.ToDictionary(
                        kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;
                Dictionary<EffectType, double>? modifyPenetrations = raw.Modify.Penetration.Count > 0
                    ? raw.Modify.Penetration.ToDictionary(
                        kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;

                yield return new BattleModifier(
                    raw.Id, raw.Name, raw.Description,
                    Set: scalarSet.Count > 0 ? scalarSet : null,
                    Modify: scalarModify.Count > 0 ? scalarModify : null,
                    SetResistances: setResistances,
                    ModifyResistances: modifyResistances,
                    SetPenetrations: setPenetrations,
                    ModifyPenetrations: modifyPenetrations,
                    AddDamagePerHit: raw.Add.DamagePerHit.Count == 0
                        ? null
                        : raw.Add.DamagePerHit
                            .Select(c => new DamageComponent(
                                c.DamageType != null ? Enum.Parse<EffectType>(c.DamageType, ignoreCase: true) : (EffectType?)null,
                                c.Scaling.Select(s => new DamageScaling(s.Stat, s.Scale)).ToArray()))
                            .ToArray());
            }
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

                    // Apply modifier action groups in deterministic order: Set → Modify → Add.
                    // Set: last modifier with a given key wins (override/replace).
                    var setCost = GetLastSet<int>(compiledMods, ModifierVariable.Cost, sk.Cost);
                    var setDmgMult = GetLastSet<double>(compiledMods, ModifierVariable.DamageMultiplier, sk.DamageMultiplier);
                    var setIsAoe = GetLastSet<bool>(compiledMods, ModifierVariable.IsAoe, sk.IsAoe);
                    var setCooldown = GetLastSet<int>(compiledMods, ModifierVariable.Cooldown, sk.Cooldown);
                    var setInitCd = GetLastSet<int>(compiledMods, ModifierVariable.InitialCooldown, sk.InitialCooldown);

                    // Modify: all deltas for a key are summed on top of the Set result.
                    var extraComponents = compiledMods
                        .Where(m => m.AddDamagePerHit != null && sk.Category != Battle.SkillCategory.Spell)
                        .SelectMany(m => m.AddDamagePerHit!)
                        .ToArray();

                    return sk with
                    {
                        Modifiers = compiledMods.Select(m => m.Id).ToArray(),
                        Cost = setCost + SumModifyInt(compiledMods, ModifierVariable.Cost),
                        DamageMultiplier = setDmgMult + SumModify(compiledMods, ModifierVariable.DamageMultiplier),
                        IsAoe = setIsAoe,
                        Cooldown = setCooldown + SumModifyInt(compiledMods, ModifierVariable.Cooldown),
                        InitialCooldown = setInitCd + SumModifyInt(compiledMods, ModifierVariable.InitialCooldown),
                        Effects = extraComponents.Length == 0 || sk.Effects.Count == 0
                            ? sk.Effects
                            : new SkillEffect[]
                                {
                                    new SkillEffect(
                                        sk.Effects[0].Kind,
                                        sk.Effects[0].Target,
                                        sk.Effects[0].DamagePerHit.Concat(extraComponents).ToArray())
                                }.Concat(sk.Effects.Skip(1)).ToArray(),
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

            // Compile and apply unit-level modifiers to resistances.
            var unitMods = raw.Modifiers
                .Select(modId => modifierDict.TryGetValue(modId, out var mod)
                    ? mod
                    : throw new KeyNotFoundException(
                        $"Unit '{raw.Id}' references unknown modifier '{modId}'."))
                .ToArray();

            int disruptionResistance = 0;
            if (unitMods.Length > 0)
            {
                var mergedResistances = resistances != null
                    ? new Dictionary<EffectType, int>(resistances)
                    : new Dictionary<EffectType, int>();

                foreach (EffectType type in new[] { EffectType.Physical, EffectType.Fire, EffectType.Cold, EffectType.Lightning, EffectType.Holy, EffectType.Void })
                {
                    int baseVal = mergedResistances.TryGetValue(type, out int r) ? r : 0;
                    int finalVal = GetLastSetResistance(unitMods, type, baseVal) + SumModifyResistance(unitMods, type);
                    if (finalVal != 0)
                        mergedResistances[type] = finalVal;
                    else
                        mergedResistances.Remove(type);
                }

                resistances = mergedResistances.Count > 0 ? mergedResistances : null;

                disruptionResistance = GetLastSet<int>(unitMods, ModifierVariable.DisruptionResistance, 0)
                    + SumModifyInt(unitMods, ModifierVariable.DisruptionResistance);
            }

            IReadOnlyDictionary<EffectType, int>? penetrations = null;
            int disruptionPenetration = 0;
            if (unitMods.Length > 0)
            {
                var mergedPenetrations = new Dictionary<EffectType, int>();

                foreach (EffectType type in new[] { EffectType.Physical, EffectType.Fire, EffectType.Cold, EffectType.Lightning, EffectType.Holy, EffectType.Void })
                {
                    int finalVal = GetLastSetPenetration(unitMods, type) + SumModifyPenetration(unitMods, type);
                    if (finalVal != 0)
                        mergedPenetrations[type] = finalVal;
                }

                penetrations = mergedPenetrations.Count > 0 ? mergedPenetrations : null;

                disruptionPenetration = GetLastSet<int>(unitMods, ModifierVariable.DisruptionPenetration, 0)
                    + SumModifyInt(unitMods, ModifierVariable.DisruptionPenetration);
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
                Resistances: resistances,
                DisruptionResistance: disruptionResistance,
                Penetrations: penetrations,
                DisruptionPenetration: disruptionPenetration);
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
                BaseHits: raw.BaseHits,
                ScalingHits: raw.ScalingHits.Count == 0
                    ? null
                    : raw.ScalingHits.Select(s => new DamageScaling(s.Stat, s.Scale)).ToArray<DamageScaling>(),
                Range: Enum.Parse<SkillRange>(raw.Range, ignoreCase: true),
                Category: Enum.Parse<SkillCategory>(raw.Category, ignoreCase: true));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the value for <paramref name="key"/> from the last modifier in
        /// <paramref name="mods"/> that has a Set entry for that key, or <paramref name="fallback"/>
        /// if none does. Converts via <see cref="System.Convert.ChangeType"/> to handle YAML
        /// type variance (e.g. int stored as string).
        /// </summary>
        private static T GetLastSet<T>(IReadOnlyList<BattleModifier> mods, ModifierVariable key, T fallback)
        {
            for (int i = mods.Count - 1; i >= 0; i--)
            {
                if (mods[i].Set != null && mods[i].Set!.TryGetValue(key, out var val))
                {
                    try
                    {
                        return (T)System.Convert.ChangeType(val, typeof(T));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Modifier '{mods[i].Id}': cannot convert Set['{key}'] value '{val}' ({val?.GetType().Name}) to {typeof(T).Name}.",
                            ex);
                    }
                }
            }
            return fallback;
        }

        /// <summary>Sums all Modify deltas across modifiers for the given key.</summary>
        private static double SumModify(IReadOnlyList<BattleModifier> mods, ModifierVariable key)
        {
            double total = 0;
            foreach (var m in mods)
                if (m.Modify != null && m.Modify.TryGetValue(key, out var delta))
                    total += delta;
            return total;
        }

        /// <summary>Sums Modify deltas as an integer (rounds to nearest).</summary>
        private static int SumModifyInt(IReadOnlyList<BattleModifier> mods, ModifierVariable key) =>
            (int)Math.Round(SumModify(mods, key));

        // ── Typed resistance helpers ─────────────────────────────────────────

        private static int GetLastSetResistance(IReadOnlyList<BattleModifier> mods, EffectType type, int fallback)
        {
            for (int i = mods.Count - 1; i >= 0; i--)
                if (mods[i].SetResistances != null && mods[i].SetResistances!.TryGetValue(type, out int val))
                    return val;
            return fallback;
        }

        private static int SumModifyResistance(IReadOnlyList<BattleModifier> mods, EffectType type)
        {
            double total = 0;
            foreach (var m in mods)
                if (m.ModifyResistances != null && m.ModifyResistances.TryGetValue(type, out double delta))
                    total += delta;
            return (int)Math.Round(total);
        }

        // ── Typed penetration helpers ────────────────────────────────────────

        private static int GetLastSetPenetration(IReadOnlyList<BattleModifier> mods, EffectType type)
        {
            for (int i = mods.Count - 1; i >= 0; i--)
                if (mods[i].SetPenetrations != null && mods[i].SetPenetrations!.TryGetValue(type, out int val))
                    return val;
            return 0;
        }

        private static int SumModifyPenetration(IReadOnlyList<BattleModifier> mods, EffectType type)
        {
            double total = 0;
            foreach (var m in mods)
                if (m.ModifyPenetrations != null && m.ModifyPenetrations.TryGetValue(type, out double delta))
                    total += delta;
            return (int)Math.Round(total);
        }

        private static T ParseYaml<T>(string yamlText)
        {
            using var reader = new StringReader(yamlText);
            return _deserializer.Deserialize<T>(reader);
        }
    }
}
