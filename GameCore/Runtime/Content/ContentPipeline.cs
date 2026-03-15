using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore.Battle;
using GameCore.Content.Raw;
#if !UNITY_5_3_OR_NEWER
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
#endif

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
#if !UNITY_5_3_OR_NEWER
        private static readonly IDeserializer _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// Loads and compiles all content accessed through <paramref name="source"/>.
        /// </summary>
        public static ContentDatabase Load(IContentSource source)
        {
            var buffDefs = LoadBuffDefinitions(source);
            var tagRules = LoadModifierTagRules(source);
            var modifiers = LoadModifiers(source).ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
            var skills = LoadSkills(source, buffDefs).ToDictionary(s => s.Id);
            var units = LoadUnits(source, skills, modifiers, tagRules);
            return new ContentDatabase(units, skills.Values, modifiers.Values, buffDefs);
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
                // ── Scalar Set (skill variables) ──────────────────────────────
                var scalarSet = new Dictionary<ModifierVariable, object>();
                if (raw.Set.Cost != null) scalarSet[ModifierVariable.Cost] = raw.Set.Cost.Value;
                if (raw.Set.DamageMultiplier != null) scalarSet[ModifierVariable.DamageMultiplier] = raw.Set.DamageMultiplier.Value;
                if (raw.Set.IsAoe != null) scalarSet[ModifierVariable.IsAoe] = raw.Set.IsAoe.Value;
                if (raw.Set.Cooldown != null) scalarSet[ModifierVariable.Cooldown] = raw.Set.Cooldown.Value;
                if (raw.Set.InitialCooldown != null) scalarSet[ModifierVariable.InitialCooldown] = raw.Set.InitialCooldown.Value;
                if (raw.Set.ExtraHits != null) scalarSet[ModifierVariable.ExtraHits] = raw.Set.ExtraHits.Value;

                // ── Scalar Modify ─────────────────────────────────────────────
                var scalarModify = new Dictionary<ModifierVariable, double>();
                if (raw.Modify.Cost.HasValue) scalarModify[ModifierVariable.Cost] = raw.Modify.Cost.Value;
                if (raw.Modify.DamageMultiplier.HasValue) scalarModify[ModifierVariable.DamageMultiplier] = raw.Modify.DamageMultiplier.Value;
                if (raw.Modify.Cooldown.HasValue) scalarModify[ModifierVariable.Cooldown] = raw.Modify.Cooldown.Value;
                if (raw.Modify.InitialCooldown.HasValue) scalarModify[ModifierVariable.InitialCooldown] = raw.Modify.InitialCooldown.Value;
                if (raw.Modify.ExtraHits.HasValue) scalarModify[ModifierVariable.ExtraHits] = raw.Modify.ExtraHits.Value;

                // ── CC resistance/penetration from Resistance/Penetration dicts ─
                // CC keys (e.g. "disruption") are extracted before the remaining
                // entries are parsed as elemental EffectType values.
                var setResKvp = raw.Set.Resistance.FirstOrDefault(kvp => kvp.Key.Equals("disruption", StringComparison.OrdinalIgnoreCase));
                if (setResKvp.Key != null) scalarSet[ModifierVariable.DisruptionResistance] = setResKvp.Value;
                var setPenKvp = raw.Set.Penetration.FirstOrDefault(kvp => kvp.Key.Equals("disruption", StringComparison.OrdinalIgnoreCase));
                if (setPenKvp.Key != null) scalarSet[ModifierVariable.DisruptionPenetration] = setPenKvp.Value;
                var modResKvp = raw.Modify.Resistance.FirstOrDefault(kvp => kvp.Key.Equals("disruption", StringComparison.OrdinalIgnoreCase));
                if (modResKvp.Key != null) scalarModify[ModifierVariable.DisruptionResistance] = modResKvp.Value;
                var modPenKvp = raw.Modify.Penetration.FirstOrDefault(kvp => kvp.Key.Equals("disruption", StringComparison.OrdinalIgnoreCase));
                if (modPenKvp.Key != null) scalarModify[ModifierVariable.DisruptionPenetration] = modPenKvp.Value;

                // ── Typed resistance/penetration dictionaries (elemental only) ──
                var setResElemental = raw.Set.Resistance.Where(kvp => Enum.TryParse<EffectType>(kvp.Key, ignoreCase: true, out _)).ToList();
                Dictionary<EffectType, int>? setResistances = setResElemental.Count > 0
                    ? setResElemental.ToDictionary(kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;
                var modResElemental = raw.Modify.Resistance.Where(kvp => Enum.TryParse<EffectType>(kvp.Key, ignoreCase: true, out _)).ToList();
                Dictionary<EffectType, double>? modifyResistances = modResElemental.Count > 0
                    ? modResElemental.ToDictionary(kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;
                var setPenElemental = raw.Set.Penetration.Where(kvp => Enum.TryParse<EffectType>(kvp.Key, ignoreCase: true, out _)).ToList();
                Dictionary<EffectType, int>? setPenetrations = setPenElemental.Count > 0
                    ? setPenElemental.ToDictionary(kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
                    : null;
                var modPenElemental = raw.Modify.Penetration.Where(kvp => Enum.TryParse<EffectType>(kvp.Key, ignoreCase: true, out _)).ToList();
                Dictionary<EffectType, double>? modifyPenetrations = modPenElemental.Count > 0
                    ? modPenElemental.ToDictionary(kvp => Enum.Parse<EffectType>(kvp.Key, ignoreCase: true), kvp => kvp.Value)
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
                                c.Scaling.Select(s => new DamageScaling(s.Stat, s.Scale)).ToArray(),
                                c.BuildupPower))
                            .ToArray(),
                    ExclusiveWith: raw.ExclusiveWith.Count > 0 ? raw.ExclusiveWith.ToArray() : null,
                    Tags: raw.Tags.Count > 0 ? raw.Tags.ToArray() : null,
                    SetCategory: raw.Set.Category != null
                        ? Enum.Parse<SkillCategory>(raw.Set.Category, ignoreCase: true)
                        : (SkillCategory?)null,
                    Trigger: raw.Trigger != null
                        ? Enum.Parse<ReactionTrigger>(raw.Trigger, ignoreCase: true)
                        : (ReactionTrigger?)null,
                    TriggerConditions: raw.TriggerConditions?.Select(c => new TriggerCondition(
                        Range: c.Range != null ? Enum.Parse<SkillRange>(c.Range, ignoreCase: true) : (SkillRange?)null,
                        DamageType: c.DamageType != null ? Enum.Parse<EffectType>(c.DamageType, ignoreCase: true) : (EffectType?)null
                    )).ToArray());
            }
        }

        // ── Skills ────────────────────────────────────────────────────────────

        private static IReadOnlyDictionary<string, ActiveEffectDefinition> LoadBuffDefinitions(IContentSource source)
        {
            const string path = "buff-definitions.yml";
            if (!source.FileExists(path))
                return new Dictionary<string, ActiveEffectDefinition>();

            var list = ParseYaml<List<Raw.RawBuffDefinition>>(source.ReadAllText(path));
            var result = new Dictionary<string, ActiveEffectDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in list)
                result[raw.Id] = CompileBuffDefinition(raw);
            return result;
        }

        private static ActiveEffectDefinition CompileBuffDefinition(Raw.RawBuffDefinition raw)
        {
            var durationKind = Enum.Parse<EffectDurationKind>(raw.DurationKind, ignoreCase: true);
            var stackingPolicy = Enum.Parse<EffectStackingPolicy>(raw.StackingPolicy, ignoreCase: true);
            var s = raw.Stats;

            var statModifiers = new List<RuntimeStatModifier>();
            if (s.ReceivingHealingMultiplier.HasValue)
                statModifiers.Add(new RuntimeStatModifier(RuntimeStatKey.ReceivingHealingMultiplier, ModifierOperation.Multiply, s.ReceivingHealingMultiplier.Value));
            if (s.ReceivingBarrierMultiplier.HasValue)
                statModifiers.Add(new RuntimeStatModifier(RuntimeStatKey.ReceivingBarrierMultiplier, ModifierOperation.Multiply, s.ReceivingBarrierMultiplier.Value));

            Dictionary<EffectType, double>? damageDealt = null;
            if (s.DamageDealtMultiplier != null)
                foreach (var entry in s.DamageDealtMultiplier)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (damageDealt == null) damageDealt = new Dictionary<EffectType, double>();
                            damageDealt[et] = damageDealt.TryGetValue(et, out double prev) ? prev * kvp.Value : kvp.Value;
                        }

            Dictionary<EffectType, double>? damageTaken = null;
            if (s.DamageTakenMultiplier != null)
                foreach (var entry in s.DamageTakenMultiplier)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (damageTaken == null) damageTaken = new Dictionary<EffectType, double>();
                            damageTaken[et] = damageTaken.TryGetValue(et, out double prev) ? prev * kvp.Value : kvp.Value;
                        }

            Dictionary<EffectType, int>? resistance = null;
            if (s.Resistance != null)
                foreach (var entry in s.Resistance)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (resistance == null) resistance = new Dictionary<EffectType, int>();
                            resistance[et] = (resistance.TryGetValue(et, out int prev) ? prev : 0) + kvp.Value;
                        }

            Dictionary<EffectType, int>? penetration = null;
            if (s.Penetration != null)
                foreach (var entry in s.Penetration)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (penetration == null) penetration = new Dictionary<EffectType, int>();
                            penetration[et] = (penetration.TryGetValue(et, out int prev) ? prev : 0) + kvp.Value;
                        }

            return new ActiveEffectDefinition(
                Id: raw.Id,
                Name: raw.Name ?? raw.Id,
                DurationKind: durationKind,
                Duration: raw.Duration,
                StackingPolicy: stackingPolicy,
                StatModifiers: statModifiers.Count > 0 ? statModifiers.ToArray() : null,
                DamageDealtMultiplierByType: damageDealt,
                DamageTakenMultiplierByType: damageTaken,
                ResistanceModifierByType: resistance,
                PenetrationModifierByType: penetration,
                Alignment: raw.IsDebuff ? EffectAlignment.Debuff : EffectAlignment.Buff);
        }

        private static IEnumerable<BattleSkill> LoadSkills(IContentSource source, IReadOnlyDictionary<string, ActiveEffectDefinition> buffDefs)
        {
            const string dir = "skills";
            if (!source.DirectoryExists(dir))
                yield break;

            foreach (var file in source.ListFiles(dir, "*.yml"))
            {
                var list = ParseYaml<List<RawSkill>>(source.ReadAllText(file));
                foreach (var raw in list)
                    yield return CompileSkill(raw, buffDefs);
            }
        }

        // ── Units ─────────────────────────────────────────────────────────────

        private static IEnumerable<BattleUnit> LoadUnits(
            IContentSource source, IReadOnlyDictionary<string, BattleSkill> skillDict,
            IReadOnlyDictionary<string, BattleModifier> modifierDict,
            IReadOnlyList<RawModifierTagRule> tagRules)
        {
            const string dir = "units";
            if (!source.DirectoryExists(dir))
                yield break;

            foreach (var file in source.ListFiles(dir, "*.yml"))
            {
                var raw = ParseYaml<RawUnit>(source.ReadAllText(file));
                yield return CompileUnit(raw, skillDict, modifierDict, tagRules);
            }
        }

        private static IReadOnlyList<RawModifierTagRule> LoadModifierTagRules(IContentSource source)
        {
            const string path = "modifier-tag-rules.yml";
            if (!source.FileExists(path))
                return Array.Empty<RawModifierTagRule>();
            return ParseYaml<List<RawModifierTagRule>>(source.ReadAllText(path));
        }
#endif

        private static BattleUnit CompileUnit(RawUnit raw, IReadOnlyDictionary<string, BattleSkill> skillDict,
            IReadOnlyDictionary<string, BattleModifier> modifierDict, IReadOnlyList<RawModifierTagRule> tagRules)
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

                    // Validate exclusive tags:
                    foreach (var mod in compiledMods)
                    {
                        if (mod.ExclusiveWith == null) continue;
                        foreach (var exclusiveTag in mod.ExclusiveWith)
                        {
                            var conflicting = compiledMods.FirstOrDefault(
                                m2 => !ReferenceEquals(m2, mod) && m2.Tags != null &&
                                m2.Tags.Any(t => string.Equals(t, exclusiveTag, StringComparison.OrdinalIgnoreCase)));
                            if (conflicting != null)
                                throw new InvalidOperationException(
                                    $"Unit '{raw.Id}' skill slot '{slot.Id}': modifier '{mod.Id}' is exclusive with tag '{exclusiveTag}' (carried by '{conflicting.Id}').");
                        }
                    }

                    // Collect all tags from applied modifiers for tag-based skill properties.
                    var allTags = compiledMods
                        .Where(m => m.Tags != null)
                        .SelectMany(m => m.Tags!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    // Apply modifier action groups in deterministic order: Set → Modify → Add.
                    // Set: last modifier with a given key wins (override/replace).
                    var setCost = GetLastSet<int>(compiledMods, ModifierVariable.Cost, sk.Cost);
                    var setDmgMult = GetLastSet<double>(compiledMods, ModifierVariable.DamageMultiplier, sk.DamageMultiplier);
                    var setIsAoe = GetLastSet<bool>(compiledMods, ModifierVariable.IsAoe, sk.IsAoe);
                    var setCooldown = GetLastSet<int>(compiledMods, ModifierVariable.Cooldown, sk.Cooldown);
                    var setInitCd = GetLastSet<int>(compiledMods, ModifierVariable.InitialCooldown, sk.InitialCooldown);
                    var setExtraHits = GetLastSet<double>(compiledMods, ModifierVariable.ExtraHits, sk.BaseHits);
                    var setCategory = compiledMods.Select(m => m.SetCategory).FirstOrDefault(c => c != null);

                    // Modify: all deltas for a key are summed on top of the Set result.
                    var extraComponents = compiledMods
                        .Where(m => m.AddDamagePerHit != null && sk.Category != Battle.SkillCategory.Spell)
                        .SelectMany(m => m.AddDamagePerHit!)
                        .ToArray();

                    return sk with
                    {
                        Modifiers = compiledMods.Select(m => m.Id).ToArray(),
                        ModifierTags = allTags.Length > 0 ? (IReadOnlyList<string>)allTags : null,
                        Category = setCategory.HasValue ? setCategory.Value : sk.Category,
                        Trigger = compiledMods.Select(m => m.Trigger).FirstOrDefault(t => t != null) ?? sk.Trigger,
                        TriggerConditions = compiledMods.Select(m => m.TriggerConditions).FirstOrDefault(tc => tc != null) ?? sk.TriggerConditions,
                        Cost = setCost + SumModifyInt(compiledMods, ModifierVariable.Cost),
                        DamageMultiplier = setDmgMult + SumModify(compiledMods, ModifierVariable.DamageMultiplier),
                        IsAoe = setIsAoe,
                        Cooldown = setCooldown + SumModifyInt(compiledMods, ModifierVariable.Cooldown),
                        InitialCooldown = setInitCd + SumModifyInt(compiledMods, ModifierVariable.InitialCooldown),
                        BaseHits = Math.Max(0.5, setExtraHits + SumModify(compiledMods, ModifierVariable.ExtraHits)),
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

                foreach (EffectType type in (EffectType[])Enum.GetValues(typeof(EffectType)))
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

            int thermalProtection = 0;
            if (unitMods.Length > 0)
            {
                thermalProtection = GetLastSet<int>(unitMods, ModifierVariable.ThermalProtection, 0)
                    + SumModifyInt(unitMods, ModifierVariable.ThermalProtection);
            }

            IReadOnlyDictionary<EffectType, int>? penetrations = null;
            int disruptionPenetration = 0;
            if (unitMods.Length > 0)
            {
                var mergedPenetrations = new Dictionary<EffectType, int>();

                foreach (EffectType type in (EffectType[])Enum.GetValues(typeof(EffectType)))
                {
                    int finalVal = GetLastSetPenetration(unitMods, type) + SumModifyPenetration(unitMods, type);
                    if (finalVal != 0)
                        mergedPenetrations[type] = finalVal;
                }

                penetrations = mergedPenetrations.Count > 0 ? mergedPenetrations : null;

                disruptionPenetration = GetLastSet<int>(unitMods, ModifierVariable.DisruptionPenetration, 0)
                    + SumModifyInt(unitMods, ModifierVariable.DisruptionPenetration);
            }

            // ── Apply passive skill bonuses ───────────────────────────────────
            // Passive skills in the unit's skill list contribute penetrations and
            // resistances directly to the compiled unit stats.
            foreach (var skill in skills)
            {
                if (skill.Category != Battle.SkillCategory.Passive)
                    continue;

                if (skill.PassivePenetrations != null && skill.PassivePenetrations.Count > 0)
                {
                    var merged = penetrations != null
                        ? new Dictionary<EffectType, int>(penetrations)
                        : new Dictionary<EffectType, int>();
                    foreach (var kvp in skill.PassivePenetrations)
                        merged[kvp.Key] = (merged.TryGetValue(kvp.Key, out int existing) ? existing : 0) + kvp.Value;
                    penetrations = merged;
                }
                disruptionPenetration += skill.PassiveDisruptionPenetration;

                if (skill.PassiveResistances != null && skill.PassiveResistances.Count > 0)
                {
                    var merged = resistances != null
                        ? new Dictionary<EffectType, int>(resistances)
                        : new Dictionary<EffectType, int>();
                    foreach (var kvp in skill.PassiveResistances)
                        merged[kvp.Key] = (merged.TryGetValue(kvp.Key, out int existing) ? existing : 0) + kvp.Value;
                    resistances = merged;
                }
                disruptionResistance += skill.PassiveDisruptionResistance;
            }

            // ── Validate modifier tag counts per unit ─────────────────────────
            // Each tag rule requires exactly requiredPerUnit skills to carry that tag.
            foreach (var rule in tagRules)
            {
                int count = 0;
                foreach (var skill in skills)
                {
                    if (skill.ModifierTags == null) continue;
                    foreach (var tag in skill.ModifierTags)
                    {
                        if (string.Equals(tag, rule.Tag, StringComparison.OrdinalIgnoreCase))
                        {
                            count++;
                            break;
                        }
                    }
                }
                if (count != rule.RequiredPerUnit)
                    throw new InvalidOperationException(
                        $"Unit '{raw.Id}' must have exactly {rule.RequiredPerUnit} skill(s) tagged '{rule.Tag}', but found {count}.");
            }

            // ── Detect reaction skill ─────────────────────────────────────────
            BattleSkill? reactionSkill = null;
            int reactionCount = 0;
            foreach (var sk in skills)
            {
                if (sk.IsReaction)
                {
                    reactionCount++;
                    reactionSkill = sk;
                }
            }
            if (reactionCount > 1)
                throw new InvalidOperationException(
                    $"Unit '{raw.Id}' has {reactionCount} reaction skills; at most 1 is allowed.");
            if (reactionSkill != null && reactionSkill.Trigger == null)
                throw new InvalidOperationException(
                    $"Unit '{raw.Id}' reaction skill '{reactionSkill.Id}' must declare a trigger (via the modifier's 'trigger' field).");
            var resolvedSkills = reactionSkill != null
                ? skills.Where(s => !s.IsReaction).ToArray()
                : skills;

            return new BattleUnit(
                raw.Id, raw.Name,
                Team: "",           // team is assigned by the scenario, not the data file
                Level: raw.Level,
                Str: raw.Str,
                Wis: raw.Wis,
                Agi: raw.Agi,
                Skills: resolvedSkills,
                Traits: traits,
                Resistances: resistances,
                DisruptionResistance: disruptionResistance,
                Penetrations: penetrations,
                DisruptionPenetration: disruptionPenetration,
                ThermalProtection: thermalProtection,
                EquipmentType: Enum.Parse<EquipmentType>(raw.EquipmentType, ignoreCase: true),
                ReactionSkill: reactionSkill);
        }

        private static BattleSkill CompileSkill(RawSkill raw, IReadOnlyDictionary<string, ActiveEffectDefinition>? buffDefs = null)
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
                    components.Add(new DamageComponent(damageType, scaling, rawComp.BuildupPower));
                }
                EffectAlignment? dispelAlignment = null;
                if (kind == EffectKind.Dispel && rawEffect.DispelAlignment != null)
                    dispelAlignment = Enum.Parse<EffectAlignment>(rawEffect.DispelAlignment, ignoreCase: true);
                effects.Add(new SkillEffect(kind, target, components, BarKey: rawEffect.BarKey, BarAmount: rawEffect.BarAmount, EffectDefinition: CompileEffectDefinition(kind, rawEffect, buffDefs), DispelAlignment: dispelAlignment));
            }

            // ── Passive stat bonuses ──────────────────────────────────────────
            IReadOnlyDictionary<EffectType, int>? passivePenetrations = null;
            int passiveDisruptionPenetration = 0;
            IReadOnlyDictionary<EffectType, int>? passiveResistances = null;
            int passiveDisruptionResistance = 0;

            if (raw.Penetration.Count > 0)
            {
                var pen = new Dictionary<EffectType, int>();
                foreach (var kvp in raw.Penetration)
                {
                    if (kvp.Key.Equals("disruption", StringComparison.OrdinalIgnoreCase))
                        passiveDisruptionPenetration = kvp.Value;
                    else if (Enum.TryParse<EffectType>(kvp.Key, ignoreCase: true, out var t))
                        pen[t] = kvp.Value;
                }
                if (pen.Count > 0)
                    passivePenetrations = pen;
            }

            if (raw.Resistance.Count > 0)
            {
                var res = new Dictionary<EffectType, int>();
                foreach (var kvp in raw.Resistance)
                {
                    if (kvp.Key.Equals("disruption", StringComparison.OrdinalIgnoreCase))
                        passiveDisruptionResistance = kvp.Value;
                    else if (Enum.TryParse<EffectType>(kvp.Key, ignoreCase: true, out var t))
                        res[t] = kvp.Value;
                }
                if (res.Count > 0)
                    passiveResistances = res;
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
                Category: Enum.Parse<SkillCategory>(raw.Category, ignoreCase: true),
                PassivePenetrations: passivePenetrations,
                PassiveDisruptionPenetration: passiveDisruptionPenetration,
                PassiveResistances: passiveResistances,
                PassiveDisruptionResistance: passiveDisruptionResistance,
                PermittedTraits: raw.PermittedTraits?.Select(t => Enum.Parse<BattleTrait>(t, ignoreCase: true)).ToArray(),
                PermittedEquipmentTypes: raw.PermittedEquipmentTypes?.Select(t => Enum.Parse<EquipmentType>(t, ignoreCase: true)).ToArray(),
                Trigger: raw.Trigger != null
                    ? Enum.Parse<ReactionTrigger>(raw.Trigger, ignoreCase: true)
                    : (ReactionTrigger?)null,
                TriggerConditions: raw.TriggerConditions?.Select(c => new TriggerCondition(
                    Range: c.Range != null ? Enum.Parse<SkillRange>(c.Range, ignoreCase: true) : (SkillRange?)null,
                    DamageType: c.DamageType != null ? Enum.Parse<EffectType>(c.DamageType, ignoreCase: true) : (EffectType?)null
                )).ToArray(),
                IsFocusCompatible: raw.IsFocusCompatible,
                FocusEffect: raw.FocusEffect != null && Enum.TryParse<FocusEffectKind>(raw.FocusEffect, ignoreCase: true, out var fek)
                    ? fek : (FocusEffectKind?)null,
                FocusEffectValue: raw.FocusEffectValue,
                IsStrSkill: raw.IsStrSkill,
                FuryDamageScale: raw.FuryDamageScale);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds an <see cref="ActiveEffectDefinition"/> from a raw skill effect when kind is ApplyEffect.
        /// Returns null for all other effect kinds.
        /// </summary>
        private static ActiveEffectDefinition? CompileEffectDefinition(EffectKind kind, Raw.RawEffect rawEffect, IReadOnlyDictionary<string, ActiveEffectDefinition>? buffDefs = null)
        {
            if (kind != EffectKind.ApplyEffect)
                return null;

            // EffectRef: resolve from the pre-compiled buff definition library
            if (rawEffect.EffectRef != null)
            {
                if (buffDefs != null && buffDefs.TryGetValue(rawEffect.EffectRef, out var buffDef))
                    return buffDef;
                throw new InvalidOperationException(
                    $"Buff definition '{rawEffect.EffectRef}' not found. Define it in buff-definitions.yml.");
            }

            if (rawEffect.EffectId == null)
                return null;

            var durationKind = Enum.Parse<EffectDurationKind>(rawEffect.DurationKind, ignoreCase: true);
            var statModifiers = new List<RuntimeStatModifier>();
            var s = rawEffect.Stats;

            // ── Flat healing / barrier multipliers ────────────────────────────
            if (s.ReceivingHealingMultiplier.HasValue)
                statModifiers.Add(new RuntimeStatModifier(RuntimeStatKey.ReceivingHealingMultiplier, ModifierOperation.Multiply, s.ReceivingHealingMultiplier.Value));
            if (s.ReceivingBarrierMultiplier.HasValue)
                statModifiers.Add(new RuntimeStatModifier(RuntimeStatKey.ReceivingBarrierMultiplier, ModifierOperation.Multiply, s.ReceivingBarrierMultiplier.Value));

            // ── Per-type damage dealt multipliers ─────────────────────────────
            Dictionary<EffectType, double>? damageDealtByType = null;
            if (s.DamageDealtMultiplier != null)
                foreach (var entry in s.DamageDealtMultiplier)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (damageDealtByType == null) damageDealtByType = new Dictionary<EffectType, double>();
                            damageDealtByType[et] = damageDealtByType.TryGetValue(et, out double prev) ? prev * kvp.Value : kvp.Value;
                        }

            // ── Per-type damage taken multipliers ─────────────────────────────
            Dictionary<EffectType, double>? damageTakenByType = null;
            if (s.DamageTakenMultiplier != null)
                foreach (var entry in s.DamageTakenMultiplier)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (damageTakenByType == null) damageTakenByType = new Dictionary<EffectType, double>();
                            damageTakenByType[et] = damageTakenByType.TryGetValue(et, out double prev) ? prev * kvp.Value : kvp.Value;
                        }

            // ── Per-type resistance (additive flat %) ─────────────────────────
            Dictionary<EffectType, int>? resistanceByType = null;
            if (s.Resistance != null)
                foreach (var entry in s.Resistance)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (resistanceByType == null) resistanceByType = new Dictionary<EffectType, int>();
                            resistanceByType[et] = (resistanceByType.TryGetValue(et, out int prev) ? prev : 0) + kvp.Value;
                        }

            // ── Per-type penetration (additive flat %) ────────────────────────
            Dictionary<EffectType, int>? penetrationByType = null;
            if (s.Penetration != null)
                foreach (var entry in s.Penetration)
                    foreach (var kvp in entry)
                        foreach (var et in ExpandDamageTypeKey(kvp.Key))
                        {
                            if (penetrationByType == null) penetrationByType = new Dictionary<EffectType, int>();
                            penetrationByType[et] = (penetrationByType.TryGetValue(et, out int prev) ? prev : 0) + kvp.Value;
                        }

            return new ActiveEffectDefinition(
                Id: rawEffect.EffectId,
                Name: rawEffect.EffectName ?? rawEffect.EffectId,
                DurationKind: durationKind,
                Duration: rawEffect.Duration,
                StackingPolicy: EffectStackingPolicy.RefreshDuration,
                StatModifiers: statModifiers.Count > 0 ? statModifiers.ToArray() : null,
                DamageDealtMultiplierByType: damageDealtByType,
                DamageTakenMultiplierByType: damageTakenByType,
                ResistanceModifierByType: resistanceByType,
                PenetrationModifierByType: penetrationByType);
        }

        /// <summary>
        /// Expands a YAML damage type key (including group keywords) into the matching EffectType values.
        /// Keywords: "allTypes" → all types; "elemental" → Fire, Cold, Lightning; "divine" → Holy, Void.
        /// Any valid EffectType name expands to just that type. Unknown keys yield an empty sequence.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<EffectType> ExpandDamageTypeKey(string key)
        {
            switch (key.ToLowerInvariant())
            {
                case "alltypes":
                    return (EffectType[])Enum.GetValues(typeof(EffectType));
                case "elemental":
                    return new[] { EffectType.Fire, EffectType.Cold, EffectType.Lightning };
                case "divine":
                    return new[] { EffectType.Holy, EffectType.Void };
                default:
                    if (Enum.TryParse<EffectType>(key, ignoreCase: true, out var et))
                        return new[] { et };
                    return System.Array.Empty<EffectType>();
            }
        }

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

#if !UNITY_5_3_OR_NEWER
        private static T ParseYaml<T>(string yamlText)
        {
            using var reader = new StringReader(yamlText);
            return _deserializer.Deserialize<T>(reader);
        }
#endif
    }
}
