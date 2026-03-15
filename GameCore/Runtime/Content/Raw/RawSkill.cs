#nullable enable
using System.Collections.Generic;
namespace GameCore.Content.Raw
{
    public class RawDamageScaling
    {
        public string Stat { get; set; } = "str";
        public double Scale { get; set; } = 1.0;
    }

    public class RawDamageComponent
    {
        public string? DamageType { get; set; }
        public int BuildupPower { get; set; } = 0; // flat CC bar power per hit, independent of damage
        public List<RawDamageScaling> Scaling { get; set; } = new List<RawDamageScaling>();
    }

    public class RawEffect
    {
        public string Kind { get; set; } = "Damage";
        public string Target { get; set; } = "Enemy";
        public List<RawDamageComponent> DamagePerHit { get; set; } = new List<RawDamageComponent>();
        public string? BarKey { get; set; }    // bar to modify when Kind=restoreBar (e.g. "mp", "focus")
        public int BarAmount { get; set; }     // positive = restore, negative = drain
        // ── ApplyEffect fields ────────────────────────────────────────────────
        public string? EffectRef { get; set; } // references a pre-compiled buff definition by ID
        public string? EffectId { get; set; }
        public string? EffectName { get; set; }
        public int Duration { get; set; }
        public string DurationKind { get; set; } = "ForTargetTurns";
        public RawEffectStats Stats { get; set; } = new RawEffectStats();
        public string? DispelAlignment { get; set; } // "Buff" or "Debuff"; required when Kind=dispel
    }

    // Per-type entries use lists of single-key dicts, e.g. [{physical: 1.2}].
    public class RawEffectStats
    {
        // ── Per-type dealt multiplier ─────────────────────────────────────────
        public List<Dictionary<string, double>>? DamageDealtMultiplier { get; set; }
        // ── Per-type taken multiplier ─────────────────────────────────────────
        public List<Dictionary<string, double>>? DamageTakenMultiplier { get; set; }
        // ── Flat (all-type) healing / barrier ─────────────────────────────────
        public double? ReceivingHealingMultiplier { get; set; }
        public double? ReceivingBarrierMultiplier { get; set; }
        // ── Per-type resistance / penetration ─────────────────────────────────
        public List<Dictionary<string, int>>? Resistance { get; set; }
        public List<Dictionary<string, int>>? Penetration { get; set; }
    }

    public class RawSkill
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Cost { get; set; }
        public double DamageMultiplier { get; set; } = 1.0;
        public double BaseHits { get; set; } = 1.0;
        public List<RawDamageScaling> ScalingHits { get; set; } = new List<RawDamageScaling>();
        public string Range { get; set; } = "Melee";    // Melee, Ranged, or Self
        public string Category { get; set; } = "Attack"; // Attack, Spell, Passive, or Reaction
        public bool IsAoe { get; set; }
        public int Cooldown { get; set; }
        public int InitialCooldown { get; set; }
        public List<RawEffect> Effects { get; set; } = new List<RawEffect>();
        public Dictionary<string, int> Penetration { get; set; } = new Dictionary<string, int>(); // passive-only
        public Dictionary<string, int> Resistance { get; set; } = new Dictionary<string, int>(); // passive-only
        public List<string>? PermittedTraits { get; set; }         // unit needs at least one
        public List<string>? PermittedEquipmentTypes { get; set; } // unit needs one of these
        public string? Trigger { get; set; }               // ReactionTrigger name (e.g. "OnHitBy")
        public List<RawTriggerCondition>? TriggerConditions { get; set; }
        public int FocusCost { get; set; }
        public bool RefundsAction { get; set; }
        public bool IsFocusCompatible { get; set; }
        public string? FocusEffect { get; set; }
        public double FocusEffectValue { get; set; }
        public string? IconDescription { get; set; } // authoring note for icon generation; not used at runtime
        // ── Fury system ────────────────────────────────────────────────────────
        public bool IsStrSkill { get; set; }    // must be set explicitly; never inferred
        public double FuryDamageScale { get; set; } // max damage bonus at full Fury (0.5 = +50%)
    }

    public class RawTriggerCondition
    {
        public string? Range { get; set; }      // e.g. "Melee", "Ranged"
        public string? DamageType { get; set; } // e.g. "Physical", "Fire"
    }
}
