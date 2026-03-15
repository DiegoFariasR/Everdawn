using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Battle;

namespace GameCore.Content
{
    /// <summary>
    /// The compiled, read-only content database for a session.
    /// Built once by <see cref="ContentPipeline"/> at startup; gameplay code only reads from here.
    /// </summary>
    public sealed class ContentDatabase
    {
        private readonly IReadOnlyDictionary<string, BattleUnit> _units;
        private readonly IReadOnlyDictionary<string, BattleSkill> _skills;
        private readonly IReadOnlyDictionary<string, BattleModifier> _modifiers;
        private readonly IReadOnlyDictionary<string, ActiveEffectDefinition> _buffDefinitions;

        internal ContentDatabase(
            IEnumerable<BattleUnit> units,
            IEnumerable<BattleSkill> skills,
            IEnumerable<BattleModifier> modifiers,
            IReadOnlyDictionary<string, ActiveEffectDefinition>? buffDefinitions = null)
        {
            _units = units.ToDictionary(u => u.Id);
            _skills = skills.ToDictionary(s => s.Id);
            _modifiers = modifiers.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
            _buffDefinitions = buffDefinitions ?? new Dictionary<string, ActiveEffectDefinition>();
        }

        /// <summary>Returns the unit with the given ID. Throws if not found.</summary>
        public BattleUnit GetUnit(string id) =>
            _units.TryGetValue(id, out var u) ? u : throw new KeyNotFoundException($"Unit '{id}' not found in content database.");

        /// <summary>Returns all units with the given IDs, in order.</summary>
        public IReadOnlyList<BattleUnit> GetUnits(IEnumerable<string> ids) =>
            ids.Select(GetUnit).ToList();

        /// <summary>All units in the database.</summary>
        public IReadOnlyCollection<BattleUnit> AllUnits => _units.Values.ToList();

        /// <summary>Returns the skill with the given ID. Throws if not found.</summary>
        public BattleSkill GetSkill(string id) =>
            _skills.TryGetValue(id, out var s) ? s : throw new KeyNotFoundException($"Skill '{id}' not found in content database.");

        /// <summary>All skills in the database.</summary>
        public IReadOnlyCollection<BattleSkill> AllSkills => _skills.Values.ToList();

        /// <summary>Returns the modifier with the given ID. Throws if not found.</summary>
        public BattleModifier GetModifier(string id) =>
            _modifiers.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException($"Modifier '{id}' not found in content database.");

        /// <summary>All modifiers in the database.</summary>
        public IReadOnlyCollection<BattleModifier> AllModifiers => _modifiers.Values.ToList();

        /// <summary>Returns the buff definition with the given ID. Throws if not found.</summary>
        public ActiveEffectDefinition GetBuffDefinition(string id) =>
            _buffDefinitions.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"Buff definition '{id}' not found in content database.");

        /// <summary>All buff definitions in the database.</summary>
        public IReadOnlyCollection<ActiveEffectDefinition> AllBuffDefinitions => _buffDefinitions.Values.ToList();
    }
}
