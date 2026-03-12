namespace GameCore.Battle;

/// <summary>
/// The category of damage dealt by a skill.
/// Used to select the correct attacker stat and to match against the defender's resistances.
/// </summary>
public enum DamageType
{
    /// <summary>Damage derived from STR — melee and ranged physical attacks.</summary>
    Physical,

    /// <summary>Damage derived from WIS — spells and magical effects.</summary>
    Magical,
}
