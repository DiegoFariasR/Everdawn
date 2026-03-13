using System.IO;
using System.Text.Json;
using GameCore;
using GameCore.Scenarios;

namespace GameCore.Tests
{
    public class InfrastructureTests
    {
        [Fact]
        public void GameInfo_HasCorrectName()
        {
            Assert.Equal("Everdawn", GameInfo.Name);
        }

        [Fact]
        public void GameInfo_HasVersion()
        {
            Assert.False(string.IsNullOrEmpty(GameInfo.Version));
        }

        [Fact]
        public void SampleScenario_HasDeterministicSeed()
        {
            var scenario = new SampleScenario();
            Assert.Equal(42, scenario.Seed);
        }

        [Fact]
        public void SampleScenario_HasStableId()
        {
            var scenario = new SampleScenario();
            Assert.Equal("sample-scenario", scenario.Id);
        }

        // ── Content index sync ────────────────────────────────────────────────
        // These tests ensure content-index.json stays in sync with the actual files
        // in GameData/Base/. Failures here mean the sandbox would serve empty responses
        // or fail to load content at runtime.

        private static string ContentBase =>
            Path.Combine(AppContext.BaseDirectory, "GameData", "Base");

        [Fact]
        public void ContentIndex_ExistsInGameData()
        {
            var indexPath = Path.Combine(ContentBase, "content-index.json");
            Assert.True(File.Exists(indexPath),
                $"content-index.json not found at: {indexPath}");
        }

        [Fact]
        public void ContentIndex_AllListedFilesExist()
        {
            var indexPath = Path.Combine(ContentBase, "content-index.json");
            var listed = JsonSerializer.Deserialize<string[]>(File.ReadAllText(indexPath))
                ?? [];

            var missing = listed
                .Where(rel => !File.Exists(Path.Combine(ContentBase, rel.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();

            Assert.True(missing.Count == 0,
                "content-index.json lists files that do not exist in GameData/Base/:\n  " +
                string.Join("\n  ", missing));
        }

        [Fact]
        public void ContentIndex_AllYamlFilesAreListed()
        {
            var indexPath = Path.Combine(ContentBase, "content-index.json");
            var listed = new HashSet<string>(
                JsonSerializer.Deserialize<string[]>(File.ReadAllText(indexPath)) ?? [],
                StringComparer.OrdinalIgnoreCase);

            var unlisted = Directory
                .EnumerateFiles(ContentBase, "*.yml", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(ContentBase, f).Replace('\\', '/'))
                .Where(rel => !listed.Contains(rel))
                .ToList();

            Assert.True(unlisted.Count == 0,
                "GameData/Base/ contains YAML files not listed in content-index.json:\n  " +
                string.Join("\n  ", unlisted));
        }
    }
}
