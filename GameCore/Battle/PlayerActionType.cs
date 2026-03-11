namespace GameCore.Battle;

public enum PlayerActionType
{
    /// <summary>Basic attack — no MP cost.</summary>
    Attack,

    /// <summary>Skill — 1.5× damage, costs 30 MP.</summary>
    Skill,

    /// <summary>Soul Burn — 2.5× damage, costs 60 MP.</summary>
    SoulBurn,
}
