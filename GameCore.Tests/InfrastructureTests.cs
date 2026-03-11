using GameCore;
using GameCore.Scenarios;

namespace GameCore.Tests;

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
}
