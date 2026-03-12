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

    /// <summary>
    /// The unit has a fury bar (max 100, starts at 0).
    /// Gains 10–50 fury per attack action. Gains 10–20 fury per incoming hit.
    /// When full, the next non-basic offensive skill is empowered (×1.5 damage)
    /// and fury resets to 0.
    /// </summary>
    Fury,
}
