#nullable enable
namespace GameCore.Battle
{
    /// <summary>
    /// A single event that occurred during a battle (attack, heal, death, start, end, etc.).
    /// <para>
    /// <b>Client contract:</b> This is the primary driver for Unity animations, VFX, and audio.
    /// The client must react to each event in <see cref="BattleStepResult.Events"/> in order,
    /// then render the final state from <see cref="BattleView"/>.
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="SkillId"/> — which animation / VFX set to play (null for system events)</item>
    ///   <item><see cref="EffectType"/> — elemental hit category (null for heals and system events)</item>
    ///   <item><see cref="HitIndex"/> / <see cref="TotalHits"/> — position in a multi-hit sequence for animation chaining</item>
    /// </list>
    /// </summary>
    public record BattleEvent(
        string ActorId,
        string Description,
        string Type,
        string? TargetId = null,
        int Value = 0,
        /// <summary>ID of the skill that produced this event. Null for system/round events.</summary>
        string? SkillId = null,
        /// <summary>Effect type of the hit. Null for heals and system events.</summary>
        EffectType? EffectType = null,
        /// <summary>0-based index of this hit within a multi-hit sequence.</summary>
        int HitIndex = 0,
        /// <summary>Total number of hits in this sequence. 1 for single-hit skills.</summary>
        int TotalHits = 1
    );
}
