namespace GameCore.Battle;

public enum BattleSkillTarget { Enemy, Ally }

/// <summary>
/// A skill that a BattleUnit can use in combat.
/// The first skill (index 0) is always free (MpCost == 0) and always available.
/// </summary>
public record BattleSkill(
    string Id,
    string Name,
    int MpCost,
    double Multiplier,
    bool IsAoe = false,
    BattleSkillTarget Target = BattleSkillTarget.Enemy,
    bool IsHeal = false,
    /// <summary>Turns this unit must wait before using this skill again. 0 = no cooldown.</summary>
    int Cooldown = 0
);
