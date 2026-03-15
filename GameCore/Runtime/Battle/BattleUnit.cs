#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
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
        IReadOnlyDictionary<EffectType, int>? Resistances = null,  // effect type → resistance %; negative = weakness
        int DisruptionResistance = 0,                               // bar mechanic; 0=none, 50=half, 100=immune, negative=weakness
        IReadOnlyDictionary<EffectType, int>? Penetrations = null,  // effect type → penetration %; reduces target resistance
        int DisruptionPenetration = 0,                              // reduces target's effective disruption resistance
        int ThermalProtection = 0,                                  // boosts fire/cold resistance for buildup only; does not affect damage reduction
        EquipmentType EquipmentType = EquipmentType.None,
        BattleSkill? ReactionSkill = null                          // fires on trigger; not in ResolvedSkills
    )
    {
        // ── Traits ───────────────────────────────────────────────────────
        public bool HasTrait(BattleTrait trait) => Traits?.Contains(trait) ?? false;

        // ── Resistances ──────────────────────────────────────────────────
        // Blunt and Slash stack additively with Physical resistance (parent stacking).
        public int GetResistance(EffectType type)
        {
            int r = Resistances != null && Resistances.TryGetValue(type, out int rv) ? rv : 0;
            // Physical sub-types inherit Physical resistance (parent stacking).
            if (type == EffectType.Blunt || type == EffectType.Slash)
                r += Resistances != null && Resistances.TryGetValue(EffectType.Physical, out int phys) ? phys : 0;
            return r;
        }

        // Blunt and Slash stack additively with Physical penetration (parent stacking).
        public int GetPenetration(EffectType type)
        {
            int p = Penetrations != null && Penetrations.TryGetValue(type, out int pv) ? pv : 0;
            // Physical sub-types inherit Physical penetration (parent stacking).
            if (type == EffectType.Blunt || type == EffectType.Slash)
                p += Penetrations != null && Penetrations.TryGetValue(EffectType.Physical, out int physPen) ? physPen : 0;
            return p;
        }

        // ── Derived stats ────────────────────────────────────────────────
        public int MaxHp => Str * 100;
        public int PhysAttack => Str * 8;
        public int MagicAttack => Wis * 8;
        public int Attack => Math.Max(PhysAttack, MagicAttack);
        // Physical, Blunt, and Slash use STR; all other types use WIS.
        public int GetBaseAttack(EffectType type) =>
            (type == EffectType.Physical || type == EffectType.Blunt || type == EffectType.Slash) ? PhysAttack : MagicAttack;
        // str → PhysAttack (Str×8), wis → MagicAttack (Wis×8), agi → Agi
        public int GetStat(string stat) => stat.ToLowerInvariant() switch
        {
            "str" => PhysAttack,
            "wis" => MagicAttack,
            "agi" => Agi,
            _ => throw new System.ArgumentException($"Unknown stat: '{stat}'"),
        };
        public EffectType NaturalEffectType =>
            MagicAttack > PhysAttack ? EffectType.Void : EffectType.Physical;
        public int Initiative => Agi;
        public int HitCount => 1 + Agi / 100;
        public IReadOnlyDictionary<string, int> MaxBars
        {
            get
            {
                var d = new Dictionary<string, int>();
                int mp = HasTrait(BattleTrait.ManaUser) ? Wis * 10 : MaxMpOverride;
                if (mp > 0) d["mp"] = mp;
                if (HasTrait(BattleTrait.FocusUser)) d["focus"] = 100;
                if (HasTrait(BattleTrait.FuryUser)) d["fury"] = 100;
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

        public IReadOnlyDictionary<string, int> InitialBars
        {
            get
            {
                var d = new Dictionary<string, int>();
                int mp = HasTrait(BattleTrait.ManaUser) ? Wis * 10 : MaxMpOverride;
                if (mp > 0) d["mp"] = mp;                         // mana starts full
                if (HasTrait(BattleTrait.FocusUser)) d["focus"] = 100; // focus starts full; regenerates per turn
                if (HasTrait(BattleTrait.FuryUser)) d["fury"] = 0;    // fury starts empty
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
                    "attack", "Attack", Cost: 0, TotalDamageMultiplier: 1.0,
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

        // Falls back to a default "Attack" skill when no skills are defined.
        public IReadOnlyList<BattleSkill> ResolvedSkills => Skills is { Count: > 0 }
            ? Skills
            : _defaultSkills;
    }
}
