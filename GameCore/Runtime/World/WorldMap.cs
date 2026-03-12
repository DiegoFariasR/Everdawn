using System.Collections.Generic;
using System.Linq;
namespace GameCore.World
{
    /// <summary>
    /// The complete collection of Locations and Activities that make up the world map.
    /// Provides stable lookup by ID. This is the read-only runtime view — populated by
    /// the content pipeline from authored data, never assembled by gameplay code directly.
    /// </summary>
    public sealed class WorldMap
    {
        private readonly Dictionary<string, Location> _locations;
        private readonly Dictionary<string, Activity> _activities;

        public WorldMap(IEnumerable<Location> locations, IEnumerable<Activity> activities)
        {
            _locations = locations.ToDictionary(l => l.Id);
            _activities = activities.ToDictionary(a => a.Id);
        }

        public IReadOnlyList<Location> Locations => _locations.Values.ToArray();

        public Location GetLocation(string id) =>
            _locations.TryGetValue(id, out var loc) ? loc
            : throw new KeyNotFoundException($"Location '{id}' not found.");

        public Activity GetActivity(string id) =>
            _activities.TryGetValue(id, out var act) ? act
            : throw new KeyNotFoundException($"Activity '{id}' not found.");

        /// <summary>Resolves the Activity objects for a given Location, in definition order.</summary>
        public IReadOnlyList<Activity> GetActivitiesFor(Location location) =>
            location.ActivityIds.Select(GetActivity).ToArray();
    }
}
