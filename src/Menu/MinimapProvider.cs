using System.Reflection;
using System.Reflection.Emit;
using UltraMode;
using UltraMode.Menu;

namespace BelmontModSettings;

/// <summary>
/// Picked up by reflection by Belmont Minimap's pause menu → MODS page (namespace, class name and the shape of
/// Describe() are its contract). Extra pages are separate in-memory assemblies — Minimap lists one row per provider.
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

    internal static Dictionary<string, object> Item(SettingsRow row)
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

/// <summary>Emits in-memory assemblies so Minimap discovers Quick Actions and Spell Tweaks as separate list rows.</summary>
internal static class ExtraProviders
{
    internal static void Emit()
    {
        Emit("UltraMode.QuickActions", typeof(ActionsProviderV2).GetMethod(nameof(ActionsProviderV2.Describe))!);
        Emit("UltraMode.SpellTweaks", typeof(SpellProviderV2).GetMethod(nameof(SpellProviderV2.Describe))!);
    }

    private static void Emit(string assemblyName, MethodInfo describe)
    {
        var asm = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var type = asm.DefineDynamicModule(assemblyName).DefineType(typeof(ProviderV2).FullName!,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
        var il = type.DefineMethod(nameof(ProviderV2.Describe), MethodAttributes.Public | MethodAttributes.Static, typeof(object[]), Type.EmptyTypes)
            .GetILGenerator();
        il.Emit(OpCodes.Call, describe);
        il.Emit(OpCodes.Ret);
        type.CreateType();
    }
}

/// <summary>Quick Actions page: D-pad swaps + Quick Cast.</summary>
public static class ActionsProviderV2
{
    public static object[] Describe()
    {
        var choices = TarotPage.ComboChoices();
        var items = new List<object>
        {
            Bool(Settings.QuickEquipWeapons, TarotPage.WeaponsZh, TarotPage.WeaponsEn, TarotPage.WeaponsHintZh, TarotPage.WeaponsHintEn),
            Bool(Settings.QuickEquipSpells, TarotPage.SpellsZh, TarotPage.SpellsEn, TarotPage.SpellsHintZh, TarotPage.SpellsHintEn),
            Bool(Settings.TarotShortcuts, TarotPage.ToggleZh, TarotPage.ToggleEn, TarotPage.ToggleHintZh, TarotPage.ToggleHintEn),
        };
        foreach (var row in TarotPage.Rows())
        {
            items.Add(new Dictionary<string, object>
            {
                ["entry"] = row.Entry,
                ["type"] = "enum",
                ["labelZh"] = row.LabelZh,
                ["labelEn"] = row.LabelEn,
                ["hintZh"] = "",
                ["hintEn"] = "",
                ["values"] = choices.Values,
                ["choicesZh"] = choices.LabelsZh,
                ["choicesEn"] = choices.LabelsEn,
            });
        }
        return new object[]
        {
            TarotPage.Id,
            TarotPage.TitleZh, TarotPage.TitleEn,
            TarotPage.SummaryZh, TarotPage.SummaryEn,
            items.ToArray(),
            (Action)SettingsRows.Save,
            (Action)TarotPage.Reset,
        };
    }

    private static Dictionary<string, object> Bool(BepInEx.Configuration.ConfigEntryBase entry, string zh, string en, string hintZh, string hintEn) =>
        new()
        {
            ["entry"] = entry,
            ["type"] = "bool",
            ["labelZh"] = zh,
            ["labelEn"] = en,
            ["hintZh"] = hintZh,
            ["hintEn"] = hintEn,
        };
}

/// <summary>Spell Tweaks page.</summary>
public static class SpellProviderV2
{
    public static object[] Describe() => new object[]
    {
        SpellPage.Id,
        SpellPage.TitleZh, SpellPage.TitleEn,
        SpellPage.SummaryZh, SpellPage.SummaryEn,
        SpellPage.Rows().Select(ProviderV2.Item).ToArray<object>(),
        (Action)SettingsRows.Save,
        (Action)SpellPage.Reset,
    };
}
