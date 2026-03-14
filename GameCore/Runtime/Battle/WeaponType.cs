namespace GameCore.Battle
{
    /// <summary>
    /// The type of weapon a unit is equipped with.
    /// Used to gate weapon-specific skills — a skill with a <see cref="WeaponType"/> requirement
    /// can only be used by a unit carrying a matching weapon.
    /// </summary>
    public enum WeaponType
    {
        /// <summary>No weapon (unarmed, or weapon type not relevant).</summary>
        None,

        /// <summary>Maces, hammers, clubs — blunt-force weapons.</summary>
        Blunt,

        /// <summary>Swords, axes — slashing weapons.</summary>
        Slash,

        /// <summary>Daggers, spears — piercing weapons.</summary>
        Pierce,

        /// <summary>Bows, crossbows — ranged projectile weapons.</summary>
        Bow,

        /// <summary>Staves, wands — arcane foci used by spellcasters.</summary>
        Staff,
    }
}
