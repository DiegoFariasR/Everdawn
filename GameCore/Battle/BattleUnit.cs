namespace GameCore.Battle;

/// <summary>
/// Immutable definition of a unit entering a battle.
/// </summary>
public record BattleUnit(
    string Id,
    string Name,
    string Team,
    int Level,
    int Str,
    int Wis,
    int Agi,
    int MaxMpOverride = 0,
    IReadOnlyList<BattleSkill>? Skills = null,
    IReadOnlyList<BattleTrait>? Traits = null,
    /// <summary>Effect type → resistance percentage. Negative values are weaknesses.</summary>
    IReadOnlyDictionary<EffectType, int>? Resistances = null
)
{
    // ── Traits ───────────────────────────────────────────────────────
    /// <summary>Returns true if this unit has the given trait.</summary>
    public bool HasTrait(BattleTrait trait) => Traits?.Contains(trait) ?? false;

    // ── Resistances ──────────────────────────────────────────────────
    /// <summary>
    /// Returns this unit's resistance percentage for <paramref name="type"/>.
    /// 0 = no mitigation. 50 = half damage. 100 = immune. Negative = weakness.
    /// </summary>
    public int GetResistance(EffectType type) =>
        Resistances != null && Resistances.TryGetValue(type, out int r) ? r : 0;

    // ── Derived stats ────────────────────────────────────────────────
    /// <summary>Max HP derived from STR.</summary>
    public int MaxHp => Str * 100;
    /// <summary>Physical damage derived from STR.</summary>
    public int PhysAttack => Str * 8;
    /// <summary>Magic damage derived from WIS.</summary>
    public int MagicAttack => Wis * 8;
    /// <summary>Effective attack power — highest of physical or magic.</summary>
    public int Attack => Math.Max(PhysAttack, MagicAttack);
    /// <summary>Returns the base attack stat for the given effect type. Physical uses STR; all other types use WIS.</summary>
    public int GetBaseAttack(EffectType type) =>
        type == EffectType.Physical ? PhysAttack : MagicAttack;
    /// <summary>The effect type that maps to this unit's highest attack stat.</summary>
    public EffectType NaturalEffectType =>
        MagicAttack > PhysAttack ? EffectType.Void : EffectType.Physical;
    /// <summary>Turn order priority derived from AGI.</summary>
    public int Initiative => Agi;
    /// <summary>Hits per action: 1 base + 1 per 100 AGI.</summary>
    public int HitCount => 1 + Agi / 100;
    /// <summary>
    /// All secondary bars this unit has (MP, Focus, Fury, …), keyed by bar name.
    /// HP is excluded — it is always tracked separately via <see cref="MaxHp"/>.
    /// Adding a new bar type only requires updating this property.
    /// </summary>
    public IReadOnlyDictionary<string, int> MaxBars
    {
        get
        {
            var d = new Dictionary<string, int>();
            int mp = HasTrait(BattleTrait.MagicUser) ? Wis * 10 : MaxMpOverride;
            if (mp > 0) d["mp"] = mp;
            if (HasTrait(BattleTrait.Focus)) d["focus"] = 100;
            if (HasTrait(BattleTrait.Fury)) d["fury"] = 100;
            return d;
        }
    }

    /// <summary>Starting values for each secondary bar (same keys as <see cref="MaxBars"/>).</summary>
    public IReadOnlyDictionary<string, int> InitialBars
    {
        get
        {
            var d = new Dictionary<string, int>();
            int mp = HasTrait(BattleTrait.MagicUser) ? Wis * 10 : MaxMpOverride;
            if (mp > 0) d["mp"] = mp;                      // mana starts full
            if (HasTrait(BattleTrait.Focus)) d["focus"] = 50;  // focus starts half
            if (HasTrait(BattleTrait.Fury)) d["fury"] = 0;  // fury starts empty
            return d;
        }
    }

    /// <summary>
    /// The unit's skill list. Always has at least one skill (the free basic action).
    /// If none were provided, a default "Attack" skill is used.
    /// </summary>
    public IReadOnlyList<BattleSkill> ResolvedSkills => Skills is { Count: > 0 }
        ? Skills
        : new BattleSkill[] { new BattleSkill("attack", "Attack", MpCost: 0, Multiplier: 1.0, Modifiers: [SkillModifier.Basic]) };
}
