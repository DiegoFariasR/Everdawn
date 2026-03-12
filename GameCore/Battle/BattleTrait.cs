namespace GameCore.Battle;

/// <summary>
/// A class-level trait that modifies a unit's battle behavior.
/// A unit can have multiple traits.
/// </summary>
public enum BattleTrait
{
    /// <summary>
    /// The unit has a mana bar. MaxMp is derived from WIS (WIS × 10).
    /// Mana regenerates at the start of each round.
    /// </summary>
    MagicUser,

    /// <summary>
    /// The unit has a focus bar (max 100, starts at 50).
    /// Gains 10 focus per offensive hit dealt. Loses 10 per incoming hit.
    /// When full, the next non-basic offensive skill is empowered (×1.5 damage)
    /// and focus resets to 50.
    /// </summary>
    Focus,
}
