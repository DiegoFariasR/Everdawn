# Architecture: GameCore Package Boundaries

This document explains the key architectural boundaries in the Everdawn codebase,
focusing on what is authoritative, what is host-specific, and how Unity consumes GameCore.

---

## What is Authoritative

**GameCore** is the single source of truth for all game logic. It is a pure C# library
with no Unity dependencies (`noEngineReferences: true` in the `.asmdef`).

- All battle resolution lives in `BattleSession` / `InteractiveBattleSession`.
- `BattleSession.RunFull()` is the single authoritative execution path for battles.
- `BattleEngine.Run()` is a thin facade that delegates directly to `BattleSession.RunFull()`.
- Tests, the web sandbox, and Unity all run through the same code path.
- Game state only changes when a validated command is accepted by `IBattleEngine`.

---

## What is Host-Specific

The following responsibilities belong to the **host** (Unity client, web sandbox, tests),
not to GameCore:

### Content Root Configuration

GameCore never searches for `GameData/` by walking directories and never reconstructs
repository layout at runtime. Each host provides content through its own
`IContentSource` implementation:

```
// Tests (GameCore.Tests/TestContentSource.cs)
// GameData/Base is copied into the test output by the project's Content items.
// Reads from the host-local output path; no repository-relative path math.
IContentSource source = TestContentSource.Default;

// Web sandbox (BattleSandbox.Web/Program.cs)
// Fetches content via HTTP from wwwroot/GameData/Base/ (static assets).
// content-index.json lists all available files; every file is pre-fetched at startup.
var contentSource = await HttpContentSource.LoadAsync(http, "GameData/Base");
builder.Services.AddSingleton<IContentSource>(contentSource);

// Unity (future)
IContentSource source = new FileSystemContentSource(
    Application.streamingAssetsPath + "/GameData/Base");
```

Each host owns its content root. No host traverses parent directories at runtime.

### Scenario Setup

`IBattleScenario.CreateSetup(IContentSource source)` requires the caller to supply
a content source. Scenarios define **which** units to use — the host decides **where**
to load them from.

### UI, Sound, VFX, Networking

All presentation concerns live in `UnityClient/`. Unity renders the `BattleView`
returned by `IBattleEngine`, but never holds its own HP or turn-state copies.

---

## IContentSource: the Content Access Boundary

`IContentSource` is the explicit boundary between host content concerns and
GameCore's content pipeline. GameCore never resolves paths or opens files on its own.

The content loading chain:

```
Host provides IContentSource
    ↓
IBattleScenario.CreateSetup(source)
    ↓
ContentPipeline.Load(source)
    ↓
ContentDatabase (compiled, read-only)
    ↓
BattleSetup (fully resolved, authoritative)
    ↓
IBattleEngine.Start(setup)
```

Host-provided implementations:
- `FileSystemContentSource` — wraps a local directory (tests, Unity editor)
- `HttpContentSource` — pre-fetches content via HTTP from `wwwroot` (web sandbox)
- Future: `ResourceContentSource` for embedded data

The `Base/Mods/` structure is preserved for future mod support:
- `GameData/Base/` — core content (always loaded)
- `GameData/Mods/` — override/extension content (loaded after Base, last modifier wins)

---

## How Unity Consumes GameCore

GameCore is a **local Unity package** referenced in `UnityClient/Packages/manifest.json`:

```json
"com.everdawn.gamecore": "file:../GameCore"
```

### Package Layout

```
GameCore/
├── package.json               ← Unity package manifest (com.everdawn.gamecore)
├── GameCore.csproj            ← .NET SDK project (netstandard2.1, LangVersion 9)
├── Runtime/                   ← All runtime source code
│   ├── GameCore.asmdef        ← Unity assembly definition (noEngineReferences: true)
│   ├── Battle/                ← Battle engine, commands, views
│   ├── Content/               ← Content pipeline, IContentSource
│   ├── Flow/                  ← Game flow state
│   ├── World/                 ← World map, locations, activities
│   ├── Contracts/             ← (future) shared interfaces and DTOs
│   ├── Events/                ← (future) EventFlow execution
│   ├── Inventory/             ← (future) item system
│   ├── Progression/           ← (future) character progression
│   ├── Quests/                ← (future) quest system
│   ├── SaveData/              ← (future) save/load
│   └── Polyfills.cs           ← C# 9 polyfill: IsExternalInit for record types
└── GameData/                  ← (see GameData/ at repo root — not inside package)
```

### Unity Compatibility

GameCore targets `netstandard2.1` and `LangVersion 9` (C# 9). The shared runtime
follows a conservative language subset that Unity's Roslyn toolchain can compile
directly without SDK tooling:

- **Block namespaces only.** File-scoped namespaces (`namespace X;`) are C# 10
  and are not used in GameCore runtime code.
- **No global using directives.** Every source file declares its own `using`
  directives explicitly. Unity compiles `.cs` files directly and never reads the
  SDK-generated `obj/` file that `ImplicitUsings` produces.
- **No implicit usings.** `ImplicitUsings` is disabled in `GameCore.csproj`.
  The project compiles correctly without any hidden imports.
- **`Polyfills.cs`** provides `IsExternalInit` — required for `init`-only properties
  and `record` types when targeting `netstandard2.1` (pre-.NET 5).

C# features intentionally **avoided** in GameCore to stay Unity-safe:
- File-scoped namespaces (C# 10) — use block namespaces instead
- Global using directives (C# 10) — use explicit per-file usings instead
- `required` members (C# 11) — use `init` properties without `required` instead
- Collection expressions `[...]` (C# 12) — use `new T[] { ... }` instead
- Primary constructors for non-record classes (C# 12)
- Raw string literals `"""` (C# 11)

CI enforces these constraints with a dedicated step:

```yaml
# Fails if file-scoped namespaces, global usings, C# 10+ syntax,
# or any namespace that relies on implicit usings is introduced.
- name: Unity compat check (LangVersion 9, no implicit usings)
  run: dotnet build GameCore/GameCore.csproj --no-restore --configuration Release
       -p:LangVersion=9 -p:ImplicitUsings=disable
```

---

## Deterministic Scenarios

Battle scenarios are defined in `GameCore.Scenarios` (not in GameCore itself, since
scenarios depend on GameData content).

- `IBattleScenario` — defines a scenario (seed, display name, setup factory)
- `IRegressionScenario` — extends with expected outcomes (winner, snapshot count)
- `ScenarioRegistry.All` — the single list shared by tests and the web sandbox

Tests and sandbox both call `BattleEngine.Run(scenario.CreateSetup(source), scenario.Seed)`,
which delegates to `BattleSession.RunFull()` — the single authoritative execution path.
Same seed + same setup = same battle, always.
