using GameCore.World;

namespace GameCore.Tests.World
{
    public class WorldMapTests
    {
        private static WorldMap MakeMap() => new(
            locations:
            [
                new Location("loc-town", "Riverside Town", ["act-inn", "act-market"]),
                new Location("loc-cave", "Dark Cave", ["act-explore"]),
            ],
            activities:
            [
                new Activity("act-inn", "Rest at the Inn"),
                new Activity("act-market", "Visit Market"),
                new Activity("act-explore", "Explore Cave"),
            ]
        );

        [Fact]
        public void GetLocation_KnownId_ReturnsLocation()
        {
            var map = MakeMap();
            var loc = map.GetLocation("loc-town");
            Assert.Equal("Riverside Town", loc.DisplayName);
        }

        [Fact]
        public void GetLocation_UnknownId_Throws()
        {
            var map = MakeMap();
            Assert.Throws<KeyNotFoundException>(() => map.GetLocation("loc-missing"));
        }

        [Fact]
        public void GetActivity_KnownId_ReturnsActivity()
        {
            var map = MakeMap();
            var act = map.GetActivity("act-inn");
            Assert.Equal("Rest at the Inn", act.DisplayName);
        }

        [Fact]
        public void GetActivity_UnknownId_Throws()
        {
            var map = MakeMap();
            Assert.Throws<KeyNotFoundException>(() => map.GetActivity("act-missing"));
        }

        [Fact]
        public void Locations_ReturnsAllLocations()
        {
            var map = MakeMap();
            Assert.Equal(2, map.Locations.Count);
        }

        [Fact]
        public void GetActivitiesFor_ReturnsActivitiesInDefinedOrder()
        {
            var map = MakeMap();
            var loc = map.GetLocation("loc-town");
            var activities = map.GetActivitiesFor(loc);
            Assert.Equal(2, activities.Count);
            Assert.Equal("act-inn", activities[0].Id);
            Assert.Equal("act-market", activities[1].Id);
        }

        [Fact]
        public void GetActivitiesFor_SingleActivity_ReturnsSingleActivity()
        {
            var map = MakeMap();
            var loc = map.GetLocation("loc-cave");
            var activities = map.GetActivitiesFor(loc);
            Assert.Single(activities);
            Assert.Equal("act-explore", activities[0].Id);
        }
    }
}
