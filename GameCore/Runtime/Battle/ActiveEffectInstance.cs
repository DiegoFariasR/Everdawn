#nullable enable
using System.Collections.Generic;
namespace GameCore.Battle
{
    /// <summary>
    /// A live instance of an active effect currently applied to a unit during battle.
    /// Carries all data needed to resolve its modifiers and tick its duration.
    /// <para>
    /// Created by <see cref="IBattleEngine.ApplyActiveEffect"/> from an
    /// <see cref="ActiveEffectDefinition"/>. Never mutates the unit's compiled base stats or skills.
    /// </para>
    /// </summary>
    public record ActiveEffectInstance(
        /// <summary>Unique runtime identifier for this instance (session-scoped).</summary>
        string InstanceId,

        /// <summary>ID of the <see cref="ActiveEffectDefinition"/> this instance was created from.</summary>
        string DefinitionId,

        /// <summary>Human-readable name (copied from the definition).</summary>
        string Name,

        /// <summary>ID of the unit that applied this effect.</summary>
        string SourceUnitId,

        /// <summary>ID of the unit this effect is applied to.</summary>
        string TargetUnitId,

        /// <summary>Remaining duration in qualifying turns. Decremented by <see cref="DurationKind"/> rules.</summary>
        int RemainingDuration,

        /// <summary>How this instance's duration counts down.</summary>
        EffectDurationKind DurationKind,

        /// <summary>Stack count. Incremented when <see cref="EffectStackingPolicy.StackIntensity"/> re-applies the same definition.</summary>
        int Stacks = 1,

        /// <summary>Optional skill modifier contributed by this instance. Null when the effect only modifies stats.</summary>
        RuntimeSkillModifier? SkillModifier = null,

        /// <summary>Stat modifiers contributed by this instance. Null when the effect only modifies skills.</summary>
        IReadOnlyList<RuntimeStatModifier>? StatModifiers = null
    );
}
