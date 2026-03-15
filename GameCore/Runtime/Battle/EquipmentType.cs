namespace GameCore.Battle
{
    /// <summary>
    /// The type of equipment a unit is carrying.
    /// Used to gate equipment-specific skills — a skill with an <see cref="EquipmentType"/> requirement
    /// can only be used by a unit carrying matching equipment.
    /// </summary>
    public enum EquipmentType
    {
        /// <summary>No relevant equipment (unarmed, or equipment type not relevant).</summary>
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
