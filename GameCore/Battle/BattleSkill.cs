namespace GameCore.Battle;

public enum BattleSkillTarget { Enemy, Ally }

/// <summary>
/// A skill that a BattleUnit can use in combat.
/// </summary>
public record BattleSkill(
    string Id,
    string Name,
    int MpCost,
    double Multiplier,
    bool IsAoe = false,
    BattleSkillTarget Target = BattleSkillTarget.Enemy,
    EffectKind Kind = EffectKind.Damage,
    /// <summary>Turns this unit must wait before using this skill again. 0 = no cooldown.</summary>
    int Cooldown = 0,
    /// <summary>Cooldown this skill starts with at the beginning of battle (explicit override).</summary>
    int InitialCooldown = 0,
    /// <summary>The elemental type this skill deals. Physical uses STR; all other types use WIS.</summary>
    EffectType EffectType = EffectType.Physical,
    /// <summary>Optional modifiers that change how this skill behaves.</summary>
    IReadOnlyList<SkillModifier>? Modifiers = null
)
{
    /// <summary>Returns true if this skill carries the given modifier.</summary>
    public bool HasModifier(SkillModifier m) => Modifiers?.Contains(m) ?? false;

    /// <summary>True if this skill heals rather than damages.</summary>
    public bool IsHeal => Kind == EffectKind.Heal;

    /// <summary>The skill is the unit's basic action (never triggers Focus empowerment).</summary>
    public bool IsBasic => HasModifier(SkillModifier.Basic);

    /// <summary>The skill is the unit's ultimate action.</summary>
    public bool IsUltimate => HasModifier(SkillModifier.Ultimate);

    /// <summary>
    /// The cooldown this skill enters battle with.
    /// Ultimate skills automatically start with at least 1 round of cooldown.
    /// </summary>
    public int EffectiveInitialCooldown => IsUltimate ? Math.Max(1, InitialCooldown) : InitialCooldown;
}
