namespace GameCore.Battle;

/// <summary>
/// Runs a deterministic turn-based battle given a setup and an RNG seed.
/// Produces a list of snapshots (one per event) for stop-motion playback.
/// </summary>
public static class BattleEngine
{
    private const int MaxRounds = 50;

    public static BattleResult Run(BattleSetup setup, int seed)
    {
        var rng = new Random(seed);
        var allUnits = setup.PlayerUnits.Concat(setup.EnemyUnits).ToList();
        var hp = allUnits.ToDictionary(u => u.Id, u => u.MaxHp);
        // bars[unitId][barKey] = current value — replaces individual mp/focus/fury dicts
        var bars = allUnits.ToDictionary(
            u => u.Id,
            u => new Dictionary<string, int>(u.InitialBars));
        int GetBar(string id, string key) => bars[id].TryGetValue(key, out int v) ? v : 0;

        IReadOnlyList<UnitState> TakeSnapshot() =>
            allUnits.Select(u => new UnitState(u.Id, hp[u.Id], hp[u.Id] > 0,
                bars[u.Id].Count > 0 ? new Dictionary<string, int>(bars[u.Id]) : null)).ToArray();

        var snapshots = new List<BattleSnapshot>();
        int step = 0;

        snapshots.Add(new BattleSnapshot
        {
            Step = step++,
            Event = new BattleEvent("system", "Battle begins!", "start"),
            UnitStates = TakeSnapshot()
        });

        var turnOrder = allUnits.OrderByDescending(u => u.Initiative).ToList();
        bool battleOver = false;

        for (int round = 1; round <= MaxRounds && !battleOver; round++)
        {
            foreach (var actor in turnOrder)
            {
                if (hp[actor.Id] <= 0) continue;

                var targets = allUnits.Where(u => u.Team != actor.Team && hp[u.Id] > 0).ToList();
                if (targets.Count == 0) { battleOver = true; break; }

                var target = targets[rng.Next(targets.Count)];
                var hitData = DamageCalc.Compute(actor, target, actor.NaturalEffectType, 1.0, 1.0, rng);
                int damage = hitData.FinalDamage;
                hp[target.Id] = Math.Max(0, hp[target.Id] - damage);
                if (actor.HasTrait(BattleTrait.Focus))
                    bars[actor.Id]["focus"] = Math.Min(100, GetBar(actor.Id, "focus") + 10);
                if (target.HasTrait(BattleTrait.Focus))
                    bars[target.Id]["focus"] = Math.Max(0, GetBar(target.Id, "focus") - 10);
                // Fury: actor gains 10–50 per action; target gains 10–20 per hit received
                if (actor.HasTrait(BattleTrait.Fury))
                    bars[actor.Id]["fury"] = Math.Min(100, GetBar(actor.Id, "fury") + rng.Next(10, 51));
                if (target.HasTrait(BattleTrait.Fury))
                    bars[target.Id]["fury"] = Math.Min(100, GetBar(target.Id, "fury") + rng.Next(10, 21));

                snapshots.Add(new BattleSnapshot
                {
                    Step = step++,
                    Event = new BattleEvent(actor.Id, $"{actor.Name} attacks {target.Name} for {damage} damage.", "attack",
                        target.Id, damage, EffectType: actor.NaturalEffectType),
                    UnitStates = TakeSnapshot()
                });

                if (hp[target.Id] <= 0)
                {
                    snapshots.Add(new BattleSnapshot
                    {
                        Step = step++,
                        Event = new BattleEvent(target.Id, $"{target.Name} is defeated!", "death"),
                        UnitStates = TakeSnapshot()
                    });
                }

                bool playerAlive = allUnits.Any(u => u.Team == "player" && hp[u.Id] > 0);
                bool enemyAlive = allUnits.Any(u => u.Team == "enemy" && hp[u.Id] > 0);
                if (!playerAlive || !enemyAlive) { battleOver = true; break; }
            }
        }

        bool playerWon = allUnits.Any(u => u.Team == "player" && hp[u.Id] > 0);
        snapshots.Add(new BattleSnapshot
        {
            Step = step,
            Event = new BattleEvent("system", playerWon ? "Victory!" : "Defeat!", "end"),
            UnitStates = TakeSnapshot()
        });

        return new BattleResult
        {
            Snapshots = snapshots,
            WinningTeam = playerWon ? "player" : "enemy",
            Seed = seed
        };
    }
}
