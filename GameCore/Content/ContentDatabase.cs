using GameCore.Battle;

namespace GameCore.Content;

/// <summary>
/// The compiled, read-only content database for a session.
/// Built once by <see cref="ContentPipeline"/> at startup; gameplay code only reads from here.
/// </summary>
public sealed class ContentDatabase
{
    private readonly IReadOnlyDictionary<string, BattleUnit> _units;

    internal ContentDatabase(IEnumerable<BattleUnit> units)
    {
        _units = units.ToDictionary(u => u.Id);
    }

    /// <summary>Returns the unit with the given ID. Throws if not found.</summary>
    public BattleUnit GetUnit(string id) =>
        _units.TryGetValue(id, out var u) ? u : throw new KeyNotFoundException($"Unit '{id}' not found in content database.");

    /// <summary>Returns all units with the given IDs, in order.</summary>
    public IReadOnlyList<BattleUnit> GetUnits(IEnumerable<string> ids) =>
        ids.Select(GetUnit).ToList();

    /// <summary>All units in the database.</summary>
    public IReadOnlyCollection<BattleUnit> AllUnits => _units.Values.ToList();
}
