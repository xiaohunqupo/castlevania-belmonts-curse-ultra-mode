using BepInEx.Configuration;

namespace UltraMode.Menu;

/// <summary>The Spell Tweaks page: changes how individual tarot spells behave.</summary>
internal static class SpellPage
{
    internal const string Id = Plugin.Id + ".spells";
    internal const string TitleZh = "奥创模式 - 法术优化", TitleEn = "Ultra Mode - Spell Tweaks";
    internal const string SummaryZh = "时空飞冲无敌、圣十字架撞墙不消失等", SummaryEn = "Invulnerable Dive, Holy Cross survives walls, and similar";

    internal static SettingsRow[] Rows() => new SettingsRow[]
    {
        Toggle(Settings.DiveInvulnerable, "时空飞冲无敌", "Invulnerable Chronomantic Dive",
            "从起跳到落地全程无敌，陷阱和即死区域仍然有效", "Takeoff to landing; traps and death zones still apply"),
        Toggle(Settings.CrossWallBounce, "圣十字架撞墙不消失", "Holy Cross survives walls",
            "撞墙继续弹射，回到身边时仍按原版回收", "Keeps bouncing off walls; catch-back is unchanged"),
    };

    internal static void Reset()
    {
        foreach (var row in Rows()) row.Entry.BoxedValue = row.Entry.DefaultValue;
    }

    private static SettingsRow Toggle(ConfigEntry<bool> entry, string labelZh, string labelEn, string hintZh, string hintEn) =>
        new(entry, labelZh, labelEn, hintZh, hintEn);
}
