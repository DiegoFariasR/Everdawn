namespace GameCore.Battle;

/// <summary>
/// A modifier that changes how a skill behaves in the battle pipeline.
/// A skill can carry multiple modifiers.
/// </summary>
public enum SkillModifier
{
    /// <summary>
    /// The unit's basic action — always available, costs no MP, and is never on cooldown.
    /// Does not trigger Focus empowerment.
    /// Produces an "attack" event type (as opposed to "skill" or "soulburn").
    /// </summary>
    Basic,

    /// <summary>
    /// The unit's ultimate — the most powerful action.
    /// Automatically starts every battle with 1 round of initial cooldown.
    /// Produces a "soulburn" event type.
    /// </summary>
    Ultimate,
}
