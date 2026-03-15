namespace GameCore.Battle
{
    // Grants a secondary bar and related battle behavior. A unit can have multiple traits.
    public enum BattleTrait
    {
        ManaUser,   // mana bar; MaxMp = WIS × 10; regenerates each round
        FocusUser,  // focus bar; builds on hits dealt, drains on hits taken; empowers next skill at 100
        FuryUser,   // fury bar; builds from damage taken and STR skills; boosts STR skill damage
    }
}
