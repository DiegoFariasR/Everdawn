using GameCore.Battle;

namespace GameCore.Tests.Battle;

public class BattleTraitTests
{
    // ── HasTrait ──────────────────────────────────────────────────────────

    [Fact]
    public void HasTrait_UnitWithTrait_ReturnsTrue()
    {
        var unit = MakeUnit(traits: [BattleTrait.MagicUser]);
        Assert.True(unit.HasTrait(BattleTrait.MagicUser));
    }

    [Fact]
    public void HasTrait_UnitWithoutTraits_ReturnsFalse()
    {
        var unit = MakeUnit(traits: null);
        Assert.False(unit.HasTrait(BattleTrait.MagicUser));
    }

    [Fact]
    public void HasTrait_EmptyTraitList_ReturnsFalse()
    {
        var unit = MakeUnit(traits: []);
        Assert.False(unit.HasTrait(BattleTrait.MagicUser));
    }

    // ── MagicUser — MaxMp ─────────────────────────────────────────────────

    [Fact]
    public void MagicUser_MaxMp_IsDerivedFromWis()
    {
        var unit = MakeUnit(wis: 100, traits: [BattleTrait.MagicUser]);
        Assert.Equal(1000, unit.MaxMp);
    }

    [Fact]
    public void MagicUser_MaxMp_IgnoresMaxMpOverride()
    {
        var unit = MakeUnit(wis: 100, maxMpOverride: 999, traits: [BattleTrait.MagicUser]);
        Assert.Equal(1000, unit.MaxMp);  // WIS * 10, not the override
    }

    [Fact]
    public void NoTrait_MaxMp_UsesMaxMpOverride()
    {
        var unit = MakeUnit(wis: 100, maxMpOverride: 50, traits: null);
        Assert.Equal(50, unit.MaxMp);
    }

    [Fact]
    public void NoTrait_NoOverride_MaxMpIsZero()
    {
        var unit = MakeUnit(wis: 100, traits: null);
        Assert.Equal(0, unit.MaxMp);
    }

    // ── Sample scenario units ─────────────────────────────────────────────

    [Fact]
    public void Mage_WithMagicUserTrait_HasPositiveMaxMp()
    {
        var mage = MakeUnit(wis: 110, traits: [BattleTrait.MagicUser]);
        Assert.Equal(1100, mage.MaxMp);
    }

    [Fact]
    public void Necromancer_WithMagicUserTrait_HasPositiveMaxMp()
    {
        var necro = MakeUnit(wis: 105, traits: [BattleTrait.MagicUser]);
        Assert.Equal(1050, necro.MaxMp);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static BattleUnit MakeUnit(
        int wis = 50,
        int maxMpOverride = 0,
        IReadOnlyList<BattleTrait>? traits = null) =>
        new("unit", "Unit", "player", Level: 1, Str: 50, Wis: wis, Agi: 50,
            MaxMpOverride: maxMpOverride, Traits: traits);
}
