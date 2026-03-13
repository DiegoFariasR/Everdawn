using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
    /// <summary>
    /// The state of a single unit captured in a snapshot.
    /// HP is tracked separately; all secondary bars (MP, Focus, Fury, …) live in <see cref="Bars"/>.
    /// <para>
    /// <b>Client contract:</b> Unity renders all fields here on every view update.
    /// Adding a new runtime state (status effect, buff, charge) only requires adding a field here
    /// and updating the relevant GameCore system that produces it.
    /// </para>
    /// </summary>
    public record UnitState(
        string UnitId,
        int CurrentHp,
        bool IsAlive,
        /// <summary>Current values for secondary bars (MP, Focus, Fury, …). Keyed by bar name.</summary>
        IReadOnlyDictionary<string, int>? Bars = null,
        /// <summary>
        /// Active status effect identifiers on this unit (e.g. "poison", "stun", "slow").
        /// Also includes definition IDs for runtime active effects (e.g. "attackUp", "guard").
        /// Empty when no effects are active. Unity renders buff/debuff icons from this list.
        /// </summary>
        IReadOnlyList<string>? StatusEffects = null,
        /// <summary>
        /// Full view of all runtime active effects on this unit.
        /// Includes remaining duration and stack count for each effect.
        /// Null when no active effects are present.
        /// </summary>
        IReadOnlyList<ActiveEffectView>? ActiveEffects = null
    )
    {
        /// <summary>Returns the current value of a named bar, or 0 if the unit does not have it.</summary>
        public int GetBar(string key) => Bars != null && Bars.TryGetValue(key, out int v) ? v : 0;

        /// <summary>True if the unit currently has the given status effect.</summary>
        public bool HasEffect(string effectId) => StatusEffects?.Contains(effectId) ?? false;
    }

    /// <summary>
    /// State of all units at a given step in the battle, paired with the event that produced it.
    /// </summary>
    public class BattleSnapshot
    {
        public int Step { get; init; }
        public BattleEvent Event { get; init; } = null!;
        public IReadOnlyList<UnitState> UnitStates { get; init; } = Array.Empty<UnitState>();
    }
}
