using System.Collections.Generic;
using GameCore.Battle;

namespace BattleSandbox.Web.Battle
{
    public record FloatNumber(string UnitId, int Value, bool IsHeal, int OffsetPct, int Key);

    public static class BattleHelpers
    {
        public static string DmgTypeCss(EffectType t) => t switch
        {
            EffectType.Physical => "badge-phys",
            EffectType.Blunt => "badge-blunt",
            EffectType.Slash => "badge-slash",
            EffectType.Fire => "badge-fire",
            EffectType.Cold => "badge-cold",
            EffectType.Lightning => "badge-lightning",
            EffectType.Holy => "badge-holy",
            EffectType.Void => "badge-void",
            _ => "badge-phys",
        };

        public static string SkillSlotClass(int index) => index switch
        {
            0 => "btn-attack",
            1 => "btn-skill",
            _ => "btn-soulburn",
        };
    }
}
