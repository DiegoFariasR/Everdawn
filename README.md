# Everdawn

A turn-based RPG with tactical combat, companion recruitment, and world exploration.

## Project Structure

| Folder | Purpose |
|---|---|
| `GameCore/` | Engine-independent game logic (battle, quests, progression, etc.) |
| `GameCore.Scenarios/` | Scenario definitions |
| `GameCore.Tests/` | Unit and integration tests |
| `UnityClient/` | Unity engine client |
| `BattleSandbox.Web/` | Web-based battle sandbox |
| `GameData/` | Game data files (base content and mods) |
| `Docs/` | Design documents and concept art |
| `.github/workflows/` | CI/CD pipelines |

## Getting Started

### Prerequisites
- [Unity](https://unity.com/) (version TBD)
- [.NET SDK](https://dotnet.microsoft.com/)
- [Git LFS](https://git-lfs.com/)

### Setup
```bash
git clone https://github.com/DiegoFariasR/Everdawn.git
cd Everdawn
git lfs pull
```
