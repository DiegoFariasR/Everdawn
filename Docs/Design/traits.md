# Traits

## Magic User
- [x] Trait implemented
- [x] Has mana bar which is blue
- [x] Max mana based on WIS (WIS × 10)
- [ ] Starts at half mana
- [x] Regens every start of round
- [ ] Group by 10 mana as a slot
- [ ] When available, spells can be empowered by 1 or more slots expended

## Focus
- [x] Trait implemented
- [ ] Has focus bar which is yellow
- [x] Fixed max 100, starts at 50
- [x] Each hit grants 10
- [x] Any damage reduces 10 focus
- [x] When full, the next non basic attack is empowered (×1.5) and resets focus to 50

## Fury
- [x] Trait implemented
- [x] Has fury bar which is red
- [x] Fixed max 100, starts at 0
- [x] Any attack grants from 10 to 50 fury
- [x] Any damage grants 10 to 20 fury
- [x] When full, the next non basic attack is empowered and empties fury

## Divine
- [ ] Trait implemented
- [ ] Has divine favors which are white filled circles
- [ ] Fixed max 5, recover all on rest/pray
- [ ] Deal less non-holy damage
- [ ] Abilities cost favors optionally to:
  - [ ] Deal extra damage as holy
  - [ ] Heals more

## Profane
- [ ] Trait implemented
- [ ] Has profane favors which are black filled circles
- [ ] Fixed max 5, recover all on rest/pray
- [ ] Deal less non-void damage
- [ ] Abilities cost favors optionally to:
  - [ ] Deal extra damage as void
  - [ ] Heals more

## Shape Shift Curse
- [ ] Trait implemented
- [ ] Transform for a time after filling some other bar
