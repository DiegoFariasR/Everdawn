# BattleSandbox.Web — Design

## Purpose

A phone-friendly web app for running and inspecting battle scenarios interactively. Hosted on GitHub Pages. Uses Blazor WebAssembly to run GameCore directly in the browser — no server required, single source of truth.

This is a **battle debugger**, not a second game client.

---

## Technology

- **Blazor WebAssembly** (standalone, no server)
- References **GameCore** and **GameCore.Scenarios** as project dependencies
- Output is fully static — deployable to GitHub Pages
- Mobile-first responsive layout

---

## Features

### Implemented
- [x] **Scenario selector** — dropdown populated from `ScenarioRegistry.All`; scenarios tagged 🎮 (playable) or 👁 (watch-only)
- [x] **Watch mode** — runs the full battle automatically and provides stop-motion playback: step forward/back, jump to start/end, click any event to jump to it
- [x] **Play mode** — interactive; player picks skills and targets each turn
  - [x] Skill buttons labelled as attack / skill / soulburn; AoE and ally-target variants handled
  - [x] Cooldown badges, MP cost display, available/unavailable states
  - [x] Single-target: select skill → click unit card to confirm target
  - [x] AoE: fires immediately on skill button click
- [x] **Auto mode** — continuously advances turns at configurable speed (Fast / Normal / Slow); toggleable mid-battle
- [x] **Take Control** — in watch mode, click "Take Control" at any step to switch to play mode from that exact battle state
- [x] **Undo** — in play mode, click any event in the log to rewind the battle to that point (replays commands deterministically)
- [x] **Unit portrait strip** — compact top bar showing all units with active/dead states highlighted
- [x] **Arena** — unit cards with HP/MP bars, stat line (STR / WIS / AGI), active and dead states
- [x] **Event log** — scrollable; current event highlighted; click-to-undo in play mode
- [x] **Floating numbers** — damage and heal numbers animate over unit cards on action
- [x] **Battle over panel** — Victory / Defeat with Retry button

### Not yet implemented (future)
- [ ] Seed display / seed override
- [ ] Scenario parameter tweaks (swap a unit, change level)
- [ ] Share link (encode scenario + seed in URL)
- [ ] Regression scenario tagging in the UI (backend interface `IRegressionScenario` exists in `GameCore.Scenarios`)

---

## Project Structure

```
BattleSandbox.Web/
  wwwroot/
    index.html          # Blazor host page
    css/
      app.css           # Mobile-first styles
  Pages/
    Home.razor          # Everything: scenario picker, play mode, watch mode
  Program.cs            # Blazor WASM entry point
  BattleSandbox.Web.csproj
```

> All UI currently lives in `Home.razor`. The `Components/` split (BattleViewer, StatePanel, EventLog) is a future refactor, not a hard requirement.

---

## How It Works

1. User opens the page on their phone
2. Blazor WASM loads (~2–5 MB on first visit, cached after)
3. User picks a scenario from the dropdown — scenarios come from `ScenarioRegistry.All` in `GameCore.Scenarios`
4. Clicking "▶ Start" runs the scenario:
   - **Watch mode** (`IsPlayable = false`): runs the full battle via `BattleEngine.Run()` and enters stop-motion playback
   - **Play mode** (`IsPlayable = true`): starts an interactive `BattleSession` and waits for player input
5. In watch mode, "⚔ Take Control" at any step resumes as an interactive session from that exact state
6. In play mode, selecting Auto ON advances turns automatically at the configured speed
7. All battle logic runs in-browser via WASM — no network calls

---

## UI Layout (Mobile Portrait)

```
┌─────────────────────────────────┐
│  Scenario: [dropdown ▼]  [▶ Start] │
│  Speed: [Fast/Normal/Slow ▼] [⚡ Auto ON/OFF] │
├─────────────────────────────────┤
│  Portrait strip: A  B  C  ⚔  X  Y  Z  W │
├─────────────────────────────────┤
│  Current event banner           │
├─────────────────────────────────┤
│  Arena                          │
│  ┌──────┐  ┌──────┐            │
│  │ Hero │  │ Enemy│  vs        │
│  │ HP ▓▓│  │ HP ░░│            │
│  │ MP ▓ │  │      │            │
│  └──────┘  └──────┘            │
├─────────────────────────────────┤
│  Action bar (play mode)         │
│  [Attack] [Skill CD2] [Ultimate]│
├── OR ───────────────────────────┤
│  Step controls (watch mode)     │
│  ◀◀  ◀  Step 3/64  ▶  ▶▶  [⚔ Take Control] │
├─────────────────────────────────┤
│  Event log (scrollable)         │
│  1. Battle begins!              │
│  2. Hero attacks Enemy (42 dmg) │
│  ► 3. Enemy uses Strike ...     │
└─────────────────────────────────┘
```

---

## Deployment

- GitHub Actions workflow builds the Blazor WASM project on push to main
- Publishes output to GitHub Pages (from a `gh-pages` branch or `/docs` output)
- No server infrastructure needed — everything is static files

---

## Dependencies

- `GameCore` — battle engine (`BattleSession`, `BattleEngine`), public API (`IBattleEngine`, `BattleView`, `BattleCommand` hierarchy)
- `GameCore.Scenarios` — scenario definitions (`ScenarioRegistry`, `IBattleScenario`, `IRegressionScenario`)
- `Microsoft.AspNetCore.Components.WebAssembly` — Blazor WASM runtime

---

## Constraints

- Must not duplicate battle logic — always runs GameCore directly
- Must not try to be a full game client
- Must work well on phone screens (portrait, touch targets ≥ 44px)
- Must remain a dev/debug tool — polish is secondary to usefulness
