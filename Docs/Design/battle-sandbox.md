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

### MVP (Phase 1)
- Scenario selector: pick from available scenarios defined in GameCore.Scenarios
- Run battle: execute the scenario with GameCore's battle engine in the browser
- Stop-motion playback:
  - Step forward / step back through battle events
  - Jump to start / jump to end
- State panel: shows unit HP, buffs, cooldowns, status at the current step
- Event log: scrollable list of all battle events with the current one highlighted
- Seed display: shows the RNG seed used

### Phase 2 (later)
- Seed override: type a custom seed and re-run
- Scenario parameter tweaks (e.g. swap a unit, change level)
- Share link: encode scenario + seed in URL for easy sharing
- Regression scenario tagging: mark a scenario as "regression" with notes

---

## Project Structure

```
BattleSandbox.Web/
  wwwroot/
    index.html          # Blazor host page
    css/
      app.css           # Mobile-first styles
  Pages/
    Home.razor          # Scenario selector + battle viewer
  Components/
    BattleViewer.razor  # Stop-motion playback controls + event display
    StatePanel.razor    # Current battle state (units, HP, buffs)
    EventLog.razor      # Scrollable event list
  Program.cs            # Blazor WASM entry point
  BattleSandbox.Web.csproj
```

---

## How It Works

1. User opens the page on their phone
2. Blazor WASM loads (~2–5 MB on first visit, cached after)
3. User picks a scenario from a dropdown (scenarios come from GameCore.Scenarios)
4. Clicking "Run" executes the battle through GameCore's battle engine (in-browser via WASM)
5. GameCore produces a list of battle events/snapshots
6. User steps through events with forward/back buttons
7. State panel updates to reflect the battle state at each step

---

## UI Layout (Mobile Portrait)

```
┌─────────────────────────┐
│  Scenario: [dropdown ▼] │
│  Seed: 42     [Run ▶]   │
├─────────────────────────┤
│                         │
│     State Panel         │
│  ┌───────┐  ┌───────┐  │
│  │ Unit A│  │ Unit B│  │
│  │ HP 80 │  │ HP 45 │  │
│  │ ...   │  │ ...   │  │
│  └───────┘  └───────┘  │
│                         │
├─────────────────────────┤
│  ◀◀  ◀  Step 3/12  ▶  ▶▶│
├─────────────────────────┤
│  Event Log              │
│  1. A attacks B (30dmg) │
│  2. B casts Shield      │
│  ► 3. A uses Fireball   │
│  4. ...                 │
└─────────────────────────┘
```

---

## Deployment

- GitHub Actions workflow builds the Blazor WASM project on push to main
- Publishes output to GitHub Pages (from a `gh-pages` branch or `/docs` output)
- No server infrastructure needed — everything is static files

---

## Dependencies

- GameCore (battle engine, game logic)
- GameCore.Scenarios (scenario definitions)
- Microsoft.AspNetCore.Components.WebAssembly (Blazor WASM runtime)

---

## Constraints

- Must not duplicate battle logic — always runs GameCore directly
- Must not try to be a full game client
- Must work well on phone screens (portrait, touch targets ≥ 44px)
- Must remain a dev/debug tool — polish is secondary to usefulness
