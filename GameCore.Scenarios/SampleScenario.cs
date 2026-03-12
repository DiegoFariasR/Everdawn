using GameCore.Battle;
using GameCore.Content;

namespace GameCore.Scenarios;

/// <summary>
/// A sample battle: three heroes vs four enemies, mirroring the reference battle image.
/// Seed 42 is fixed — same inputs always produce the same battle.
/// <para>Also implements <see cref="IRegressionScenario"/>: expected outcomes are declared here
/// and validated automatically by the test suite.</para>
/// </summary>
public class SampleScenario : IBattleScenario, IRegressionScenario
{
    public string Id => "sample-scenario";
    public string DisplayName => "Sample Battle";
    public int Seed => 42;
    public bool IsPlayable => true;

    // ── IRegressionScenario ───────────────────────────────────────────────
    // Values established from a known-good run. Update intentionally if the engine or
    // scenario changes in a way that is expected and reviewed.
    public string ExpectedWinner => "player";
    // 1 start + 56 attack/skill events + 6 death events + 1 end + 3 fury empowerment events = 67
    public int ExpectedSnapshotCount => 67;

    public override string ToString() => $"{DisplayName} [{Id}]";

    public BattleSetup CreateSetup()
    {
        var db = ContentPipeline.Load(FindGameDataBasePath());
        return new BattleSetup
        {
            PlayerUnits = db.GetUnits(["paladin", "mage", "rogue"])
                            .Select(u => u with { Team = "player" }).ToList(),
            EnemyUnits = db.GetUnits(["goblin-w", "goblin-a", "necro"])
                           .Select(u => u with { Team = "enemy" }).ToList(),
        };
    }

    /// <summary>
    /// Walks up from the running assembly's directory until it finds a folder containing
    /// <c>GameData/Base</c>. Works in tests, the web sandbox, and Unity editor.
    /// </summary>
    private static string FindGameDataBasePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "GameData", "Base");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not find GameData/Base/ by walking up from '{AppContext.BaseDirectory}'.");
    }
}
