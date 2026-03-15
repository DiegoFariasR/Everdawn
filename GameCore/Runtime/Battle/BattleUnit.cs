#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
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
        IReadOnlyDictionary<EffectType, int>? Resistances = null,
        /// <summary>
        /// Reduces disruption bar gain (0 = none, 50 = half, 100 = immune, negative = weakness).
        /// Kept separate from element resistances because disruption is a bar mechanic, not a damage type.
        /// </summary>
        int DisruptionResistance = 0,
        /// <summary>
        /// Effect type → penetration percentage. Subtracted from the target's effective resistance
        /// when this unit deals damage of that type. Positive values pierce resistance; negative values
        /// reduce effective penetration (anti-penetration).
        /// </summary>
        IReadOnlyDictionary<EffectType, int>? Penetrations = null,
        /// <summary>
        /// Reduces the target's effective disruption resistance when this unit applies disruption.
        /// Kept separate from element penetrations because disruption is a bar mechanic, not a damage type.
        /// </summary>
        int DisruptionPenetration = 0,
        /// <summary>
        /// Bonus percentage applied to fire and cold resistances when absorbing thermal buildup.
        /// 0 = no bonus. 10 = resistances act 10% stronger for buildup. Stacks additively.
        /// Example: 50% cold resistance + 10% ThermalProtection → acts like 55% for buildup.
        /// Does not affect fire/cold damage reduction — only buildup bar accumulation.
        /// </summary>
        int ThermalProtection = 0,
        /// <summary>
        /// The type of equipment this unit is carrying.
        /// Determines which equipment-gated skills the unit can use.
        /// </summary>
        EquipmentType EquipmentType = EquipmentType.None,
        /// <summary>
        /// The unit's reaction skill, if any. Fires automatically when the trigger condition is met.
        /// Not included in <see cref="ResolvedSkills"/> — it is never chosen as a battle action.
        /// At most one reaction is allowed per unit.
        /// </summary>
        BattleSkill? ReactionSkill = null
    )
    {
        // ── Traits ───────────────────────────────────────────────────────
        /// <summary>Returns true if this unit has the given trait.</summary>
        public bool HasTrait(BattleTrait trait) => Traits?.Contains(trait) ?? false;

        // ── Resistances ──────────────────────────────────────────────────
        /// <summary>
        /// Returns this unit's resistance percentage for <paramref name="type"/>.
        /// 0 = no mitigation. 50 = half damage. 100 = immune. Negative = weakness.
        /// Blunt and Slash are physical sub-types: their resistance stacks additively with Physical resistance.
        /// </summary>
        public int GetResistance(EffectType type)
        {
            int r = Resistances != null && Resistances.TryGetValue(type, out int rv) ? rv : 0;
            // Physical sub-types inherit Physical resistance (parent stacking).
            if (type == EffectType.Blunt || type == EffectType.Slash)
                r += Resistances != null && Resistances.TryGetValue(EffectType.Physical, out int phys) ? phys : 0;
            return r;
        }

        /// <summary>
        /// Returns this unit's penetration percentage for <paramref name="type"/>.
        /// Subtracted from the target's resistance before the damage formula is applied.
        /// 0 = no penetration. Positive = pierces resistance. Negative = anti-penetration.
        /// Blunt and Slash are physical sub-types: their penetration stacks additively with Physical penetration.
        /// </summary>
        public int GetPenetration(EffectType type)
        {
            int p = Penetrations != null && Penetrations.TryGetValue(type, out int pv) ? pv : 0;
            // Physical sub-types inherit Physical penetration (parent stacking).
            if (type == EffectType.Blunt || type == EffectType.Slash)
                p += Penetrations != null && Penetrations.TryGetValue(EffectType.Physical, out int physPen) ? physPen : 0;
            return p;
        }

        // ── Derived stats ────────────────────────────────────────────────
        /// <summary>Max HP derived from STR.</summary>
        public int MaxHp => Str * 100;
        /// <summary>Physical damage derived from STR.</summary>
        public int PhysAttack => Str * 8;
        /// <summary>Magic damage derived from WIS.</summary>
        public int MagicAttack => Wis * 8;
        /// <summary>Effective attack power — highest of physical or magic.</summary>
        public int Attack => Math.Max(PhysAttack, MagicAttack);
        /// <summary>Returns the base attack stat for the given effect type. Physical, Blunt, and Slash use STR; all other types use WIS.</summary>
        public int GetBaseAttack(EffectType type) =>
            (type == EffectType.Physical || type == EffectType.Blunt || type == EffectType.Slash) ? PhysAttack : MagicAttack;
        /// <summary>
        /// Returns the derived attack value for a named stat.
        /// str → PhysAttack (Str × 8), wis → MagicAttack (Wis × 8), agi → Agi.
        /// </summary>
        public int GetStat(string stat) => stat.ToLowerInvariant() switch
        {
            "str" => PhysAttack,
            "wis" => MagicAttack,
            "agi" => Agi,
            _ => throw new System.ArgumentException($"Unknown stat: '{stat}'"),
        };
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
                // Thermal bars: every unit can accumulate cold and burn.
                d[ThermalSystem.BarCold] = ThermalSystem.MaxBar;
                d[ThermalSystem.BarBurn] = ThermalSystem.MaxBar;
                // Disruption bar: every unit can accumulate disruption.
                d[DisruptionSystem.BarDisruption] = DisruptionSystem.MaxBar;
                // Bleed bar: every unit can accumulate bleed.
                d[BleedSystem.BarBleed] = BleedSystem.MaxBar;
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
                if (mp > 0) d["mp"] = mp;                         // mana starts full
                if (HasTrait(BattleTrait.Focus)) d["focus"] = 100; // focus starts full; regenerates per turn
                if (HasTrait(BattleTrait.Fury)) d["fury"] = 0;    // fury starts empty
                d[ThermalSystem.BarCold] = 0;                      // thermal bars start empty
                d[ThermalSystem.BarBurn] = 0;
                d[DisruptionSystem.BarDisruption] = 0;             // disruption bar starts empty
                d[BleedSystem.BarBleed] = 0;                       // bleed bar starts empty
                return d;
            }
        }

        private static readonly IReadOnlyList<BattleSkill> _defaultSkills =
            new BattleSkill[]
            {
                new BattleSkill(
                    "attack", "Attack", Cost: 0, DamageMultiplier: 1.0,
                    Effects: new SkillEffect[]
                    {
                        new SkillEffect(EffectKind.Damage, BattleSkillTarget.Enemy,
                            new DamageComponent[]
                            {
                                new DamageComponent(EffectType.Physical, new DamageScaling[] { new DamageScaling("str", 1.0) })
                            })
                    },
                    Modifiers: new string[] { "basic" })
            };

        /// <summary>
        /// The unit's skill list. Always has at least one skill (the free basic action).
        /// If none were provided, a default "Attack" skill is used.
        /// </summary>
        public IReadOnlyList<BattleSkill> ResolvedSkills => Skills is { Count: > 0 }
            ? Skills
            : _defaultSkills;
    }
}
