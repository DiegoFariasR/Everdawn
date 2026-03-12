using GameCore.Flow;
using GameCore.World;

namespace GameCore.Tests.Flow
{
    public class ExplorationStateTests
    {
        private static readonly Activity Inn = new("act-inn", "Rest at the Inn");
        private static readonly Activity Market = new("act-market", "Visit Market");
        private static readonly Location SingleActivityLocation = new("loc-cave", "Dark Cave", ["act-explore"]);
        private static readonly Location MultiActivityLocation = new("loc-town", "Riverside Town", ["act-inn", "act-market"]);

        [Fact]
        public void ShouldAutoEnter_OneActivity_ReturnsTrue()
        {
            var state = new ExplorationState(SingleActivityLocation, [new Activity("act-explore", "Explore Cave")]);
            Assert.True(state.ShouldAutoEnter);
        }

        [Fact]
        public void ShouldAutoEnter_MultipleActivities_ReturnsFalse()
        {
            var state = new ExplorationState(MultiActivityLocation, [Inn, Market]);
            Assert.False(state.ShouldAutoEnter);
        }

        [Fact]
        public void ShouldAutoEnter_ZeroActivities_ReturnsFalse()
        {
            var state = new ExplorationState(MultiActivityLocation, []);
            Assert.False(state.ShouldAutoEnter);
        }

        [Fact]
        public void AutoEnterActivity_OneActivity_ReturnsIt()
        {
            var explore = new Activity("act-explore", "Explore Cave");
            var state = new ExplorationState(SingleActivityLocation, [explore]);
            Assert.Equal(explore, state.AutoEnterActivity);
        }

        [Fact]
        public void AutoEnterActivity_MultipleActivities_Throws()
        {
            var state = new ExplorationState(MultiActivityLocation, [Inn, Market]);
            Assert.Throws<InvalidOperationException>(() => _ = state.AutoEnterActivity);
        }

        [Fact]
        public void AutoEnterActivity_ZeroActivities_Throws()
        {
            var state = new ExplorationState(MultiActivityLocation, []);
            Assert.Throws<InvalidOperationException>(() => _ = state.AutoEnterActivity);
        }

        [Fact]
        public void CurrentLocation_IsPreserved()
        {
            var state = new ExplorationState(MultiActivityLocation, [Inn, Market]);
            Assert.Equal(MultiActivityLocation, state.CurrentLocation);
        }

        [Fact]
        public void AvailableActivities_IsPreserved()
        {
            var state = new ExplorationState(MultiActivityLocation, [Inn, Market]);
            Assert.Equal(2, state.AvailableActivities.Count);
        }
    }
}
