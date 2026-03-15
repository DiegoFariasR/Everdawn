# Game Mechanics

- [ ] On a tavern, start with a budget, hire your heroes, go on an adventure
- [ ] World map to navigate to the POI and start a battle or sequence of battles
- [x] Battle can be set to auto
- [ ] Set to auto a route on map
- [ ] Besides battles, PoI can have challenges which can be overcome by specific talents
- [x] Cooldowns apply at the start of each round, not turn
- [x] CC bar, hard and low thresholds
- [x] Elemental and physical resistences

## Abilities

- Characters can have:
  - [x] N passive traits
  - [x] 1 passive ability
  - [x] 1 basic attack/cantrip
    - [x] 50% base effect
  - [x] 1 secondary ability
    - [x] 100% base effect
    - [x] 2 round cd
  - [x] 1 ultimate
    - [x] 150% base effect
    - [x] start with 1 round cd
    - [x] 2 round cd

- [ ] Abilities can have second cast version, can be a weaker version, a stop main effect or confirm to transformation

## Main Attributes

- **Strength - STR**
  - [x] Physical base damage
  - [x] More HP
- **Wisdom - WIS**
  - [x] Magic base damage
  - [x] More Mana
- **Agility - AGI**
  - [x] Number of hits increase
  - [x] Each 100% increase adds an extra hit instead
  - [x] Each weapon skill and spell have a base attack speed
  - [x] Initiative

## Ability Tags (one tag of each group)

### Damage Types + Keywords
- [ ] Elemental (fire, water, wind, earth)
- [ ] Celestial (holy, void)
- [ ] Physical

### Ability Types
- [x] Attack
- [x] Spell (can be silenced)
- [x] Passive

## Skill Requirements

Skills can declare requirements that a unit must meet before the skill can be used or selected by AI.

### Requirement Types
- [x] **RequiredTrait** — the unit must have a specific `BattleTrait` (e.g. `MagicUser` for spells)
- [x] **RequiredEquipmentType** — the unit must carry one of the equipment types in the list (e.g. `[Blunt]` for mace skills, or `[Blunt, Slash]` to accept multiple types)

### Equipment Types
- [x] `None` — no relevant equipment (unarmed, or equipment type not relevant)
- [x] `Blunt` — maces, hammers (mace-strike, mace-crush, mace-shatter, shield-bash)
- [x] `Slash` — swords, axes (sword-strike, sword-cleave)
- [x] `Pierce` — daggers, spears (dagger-strike, dagger-flurry, rogue-mark)
- [x] `Bow` — bows, crossbows (bow-shot, bow-precise, bow-volley)
- [x] `Staff` — staves, wands (mage and necromancer spells)

### Rules
- A skill with neither requirement is always available (no gate).
- Both requirements must be met simultaneously if both are specified.
- Unmet requirements exclude the skill from `AvailableSkillIds` (player view) and from AI skill selection.
- Attempting to use a skill with an unmet requirement returns `ValidationErrorCode.RequirementNotMet`.
- Equipment type and required trait are authored in YAML (`requiredEquipmentTypes` as a list, `requiredTrait`) and on `BattleUnit` (`equipmentType`).

## Barrier

The barrier is an energy shield that absorbs incoming damage before HP.

- [x] Barrier absorbs damage before HP; only the remainder depletes HP (same pipeline as HP damage)
- [x] Multiple grants stack additively
- [x] Decays each round: `max(5, 20 − WIS/10)%` of remaining barrier (WIS=0 → 20%/round, WIS=100 → 10%)
- [x] Heals do not restore barrier
- [x] Granted by `EffectKind.Shield` skills (e.g. `mage-barrier`, AoE, 4-turn CD, scales 2×WIS)
- [x] Displayed as light-blue overlay on HP bar (width = `min(100%, barrier × 100 / MaxHp)`)

## Status Bars — CC System

Two independent buildup-bar families drive crowd-control. Each bar runs from 0 to 100. Both families use the same design: a soft threshold (low CC) and a hard threshold (stun/freeze).

### Thermal (Cold / Burn)

- [x] `cold` bar and `burn` bar are opposing: applying cold cancels burn first; leftover builds cold
- [x] `slow` (cold ≥ 50): AGI hit count halved (base 1 hit halved separately, never below 1)
- [x] `frozen` (cold = 100): unit skips 1 turn; bar retains at 40 after trigger
- [x] `burning` (burn ≥ 50): DOT each turn (`0.5 × burn bar` damage)
- [x] Decay per turn: cold −15, burn −10
- [x] `ThermalSystem.cs` — authoritative static class with all tuning constants

### Disruption

- [x] `disruption` bar: physical impact and lightning shock; no opposition mechanic
- [x] `dizzy` (disruption ≥ 50): actor output ×0.8
- [x] `stunned` (disruption = 100): unit skips 1 turn; bar retains at 40 after trigger
- [x] Decay per turn: −20
- [x] `DisruptionPower` is an explicit per-effect opt-in value; no blanket rule by damage type
- [x] `DisruptionSystem.cs` — authoritative static class with all tuning constants

## Resistance & Penetration

- [x] Resistances: per-`EffectType` dict on `BattleUnit` (`Physical`, `Fire`, `Cold`, `Lightning`, `Holy`, `Void`)
- [x] `DisruptionResistance`: separate `int` field (not in the resistances dict)
- [x] Penetration: attacker-side per-`EffectType` dict on `BattleUnit` (`Penetrations`)
- [x] `DisruptionPenetration`: separate `int` field mirroring `DisruptionResistance`
- [x] Effective resistance = `target.GetResistance(type) − actor.GetPenetration(type)`, applied in `DamageCalc` before the damage formula
- [x] Authored in YAML via unit-level modifier lists (`set: { physicalResistance: 20 }`) and passive skill dicts (`penetration: { physical: 20 }`)

## Passive Skills

- [x] Skills with `category: Passive` grant permanent battle stat modifications for the duration of a battle
- [x] Supported YAML fields: `penetration` and `resistance` dicts (plus `disruption` key for disruption resistance/penetration)
- [x] Pipeline applies passive stats additively to the compiled `BattleUnit` after unit-level modifiers
- [x] Passive skills are never selectable as battle actions (filtered from AI selection and player input)
- [x] Each character currently has 1 passive skill (e.g. Warrior: Battle Hardening +20% physical pen; Mage: Arcane Insight +20% fire pen; Rogue: Concussion Ward +40% disruption resistance)

## Reactions

- [ ] 1 reaction skill slot per character — fires automatically on a trigger, never chosen as an action
- [ ] Reactions fire after the full triggering action resolves (not mid-hit)
- [ ] Reaction goes on cooldown after firing; CD suppresses further triggers
- [ ] Frozen/stunned units do not react; dead units do not react
- [ ] See `Docs/Design/reactions.md` for full design, trigger catalog, and planned reactions

## Open Questions

### Damage of multiple types/elements on the same attack
- Simplicity vs diversity
- Max 2?
- Max number of damage types?

### Elements Option 1
- Wind and Lightning — Speed
- Earth and Plant — HP
- Fire and Magma — Strength
- Water and Ice — Intelligence

### Elements Option 2

**Fire:**
- Controlled: Beam
- Neutral: Fire
- Chaos: Lightning

**Water:**
- Controlled: Ice
- Neutral: Water
- Chaos: Fog

**Nature:**
- Controlled: Life
- Neutral: Earth
- Chaos: Wind
