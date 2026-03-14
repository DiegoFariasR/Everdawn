#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// A runtime skill modifier applied by an active effect while it is on the acting unit.
    /// Mirrors the shape of <see cref="BattleModifier"/> for conceptual consistency, but applied
    /// at action-resolution time rather than at content-compile time.
    /// <para>
    /// Applied in the same deterministic order as the content pipeline: Set → Modify → Add.
    /// Supported Set/Modify variable keys: see <see cref="ModifierVariable"/>.
    /// </para>
    /// <para>
    /// The effective skill is resolved as an immutable overlay for the current action.
    /// The actor's compiled base skill is never permanently modified.
    /// </para>
    /// </summary>
    public record RuntimeSkillModifier(
        /// <summary>
        /// Override variable values. Applied first; when multiple Set modifiers target the same key,
        /// the last modifier (in application order) wins.
        /// </summary>
        IReadOnlyDictionary<ModifierVariable, object>? Set = null,

        /// <summary>
        /// Additive numeric deltas. Applied after Set. All deltas for a key are summed.
        /// </summary>
        IReadOnlyDictionary<ModifierVariable, double>? Modify = null,

        /// <summary>
        /// Damage components appended to the first effect's DamagePerHit after Set and Modify.
        /// Null when none.
        /// </summary>
        IReadOnlyList<DamageComponent>? AddDamagePerHit = null
    );
}
