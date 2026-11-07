using UltraMode;
using UltraMode.Menu;

namespace BelmontModSettings;

/// <summary>
/// Picked up by reflection by Belmont Minimap's pause menu → MODS page (namespace, class name and the shape of
/// Describe() are its contract).
/// </summary>
public static class ProviderV2
{
    public static object[] Describe() => new object[]
    {
        Plugin.Id,
        SettingsRows.TitleZh, SettingsRows.TitleEn,
        SettingsRows.SummaryZh, SettingsRows.SummaryEn,
        SettingsRows.All().Select(Item).ToArray<object>(),
        (Action)SettingsRows.Save,
        (Action)SettingsRows.ResetAll,
    };

    private static Dictionary<string, object> Item(SettingsRow row)
    {
        var item = new Dictionary<string, object>
        {
            ["entry"] = row.Entry,
            ["type"] = row.IsSlider ? "float" : "bool",
            ["labelZh"] = row.LabelZh,
            ["labelEn"] = row.LabelEn,
            ["hintZh"] = row.HintZh,
            ["hintEn"] = row.HintEn,
        };
        if (!row.IsSlider) return item;
        item["min"] = row.Min;
        item["max"] = row.Max;
        item["step"] = row.Step;
        item["format"] = SettingsRows.SliderFormat;
        return item;
    }
}
