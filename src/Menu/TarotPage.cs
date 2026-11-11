using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace UltraMode.Menu;

/// <summary>
/// The Quick Actions page: D-pad equip/spell swaps, then Quick Cast (on/off and one combo list per card). Combo → card
/// settings stay in <c>07 Tarot</c>; view rows mirror them so assigning a combo to a card clears it from the previous one.
/// </summary>
internal static class TarotPage
{
    internal const string Id = Plugin.Id + ".actions";
    internal const string TitleZh = "奥创模式 - 快速操作", TitleEn = "Ultra Mode - Quick Actions";
    internal const string SummaryZh = "十字键换装换法术、快速施法", SummaryEn = "D-pad equip and spell swap, Quick Cast";
    internal const string WeaponsZh = "启用左右键换装", WeaponsEn = "Enable Left/Right weapon swap";
    internal const string WeaponsHintZh = "开：十字键左右切换各类型最高级武器，十字键不再移动", WeaponsHintEn = "On: D-pad left/right cycles best owned weapon of each type; D-pad no longer moves";
    internal const string SpellsZh = "启用上下键换法术", SpellsEn = "Enable Up/Down spell swap";
    internal const string SpellsHintZh = "开：十字键上下切换未设快速施法的法术，十字键不再移动", SpellsHintEn = "On: D-pad up/down cycles spells without Quick Cast; D-pad no longer moves";
    internal const string ToggleZh = "启用快速施法", ToggleEn = "Enable Quick Cast";
    internal const string ToggleHintZh = "开：按组合键直接释放已获得的牌，消耗按那张牌自己的计算", ToggleHintEn = "On: combos cast owned cards at their own cost";

    private sealed record Combo(string Id, ConfigEntry<string> Entry, string LabelZh, string LabelEn);

    internal sealed record Row(ConfigEntry<string> Entry, string Spell, string LabelZh, string LabelEn);

    internal sealed record Choices(string[] Values, string[] LabelsZh, string[] LabelsEn);

    private static readonly string[] Spells =
    {
        "RiseAndFall", "FireSeismicWave", "GlassArmor", "MassPurification", "CursedFireBall", "PetrifyingRay",
        "GiantFist", "LaserScytheSlash", "BloodSpikes", "BoomerangCross", "Detainment", "FreezingMist",
        "InvocationStone", "LifeDrain", "PocketMirror", "TargetingChargingWheels",
    };

    private static Combo[] combos = Array.Empty<Combo>();
    private static readonly Dictionary<string, ConfigEntry<string>> views = new();
    private static bool syncing;

    internal static void Init()
    {
        combos = new Combo[]
        {
            new("Up", Settings.TarotUpSpell, "上 + Y", "Up + Y"),
            new("Down", Settings.TarotDownSpell, "下 + Y", "Down + Y"),
            new("LT", Settings.TarotLTSpell, "LT + Y", "LT + Y"),
            new("R3", Settings.TarotR3Spells, "R3", "R3"),
            new("RB", Settings.TarotRBSpell, "RB + Y", "RB + Y"),
            new("RBUp", Settings.TarotRBUpSpell, "RB + 上 + Y", "RB + Up + Y"),
            new("RBDown", Settings.TarotRBDownSpell, "RB + 下 + Y", "RB + Down + Y"),
            new("RBR3", Settings.TarotRBR3Spells, "RB + R3", "RB + R3"),
        };
        var view = new ConfigFile(Path.Combine(Paths.CachePath, "ultra_mode.tarot-page.cfg"), false) { SaveOnConfigSet = false };
        foreach (var spell in Spells)
            views[spell] = view.Bind("Cards", spell, ComboOf(spell, c => (string)c.Entry.DefaultValue), "View of 07 Tarot; not read back.");
        Sync();
        view.SettingChanged += (_, e) =>
        {
            if (syncing || e.ChangedSetting is not ConfigEntry<string> entry) return;
            Assign(entry.Definition.Key, entry.Value);
            Sync();
            RefreshMinimap();
        };
        Plugin.Instance.Config.SettingChanged += (_, e) =>
        {
            if (!syncing && combos.Any(c => c.Entry == e.ChangedSetting)) Sync();
        };
    }

    internal static Row[] Rows()
    {
        var names = Names();
        return Spells.Select(spell =>
        {
            var name = names.FirstOrDefault(n => n.Key.Contains(spell, StringComparison.OrdinalIgnoreCase)).Value ?? spell;
            return new Row(views[spell], spell, name, name);
        }).ToArray();
    }

    internal static Choices ComboChoices() => new(
        combos.Select(c => c.Id).Prepend("").ToArray(),
        combos.Select(c => c.LabelZh).Prepend("无").ToArray(),
        combos.Select(c => c.LabelEn).Prepend("None").ToArray());

    /// <summary>True once the game's card names were found, so a page built earlier with internal names can be rebuilt.</summary>
    internal static bool NamesLoaded => Names().Count > 0;

    internal static void Reset()
    {
        syncing = true;
        try
        {
            Settings.QuickEquipWeapons.BoxedValue = Settings.QuickEquipWeapons.DefaultValue;
            Settings.QuickEquipSpells.BoxedValue = Settings.QuickEquipSpells.DefaultValue;
            Settings.TarotShortcuts.BoxedValue = Settings.TarotShortcuts.DefaultValue;
            foreach (var combo in combos) combo.Entry.BoxedValue = combo.Entry.DefaultValue;
        }
        finally
        {
            syncing = false;
        }
        Sync();
        RefreshMinimap();
    }

    /// <summary>Re-reads combo → card into the view rows after the real config was reset elsewhere.</summary>
    internal static void RefreshViews()
    {
        Sync();
        RefreshMinimap();
    }

    private static bool Lists(string value, string spell) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(k => spell.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string ComboOf(string spell, Func<Combo, string> value) =>
        combos.FirstOrDefault(c => Lists(value(c), spell))?.Id ?? "";

    /// <summary>Removes the card from every other combo and gives it the picked one, replacing what that combo cast.</summary>
    private static void Assign(string spell, string comboId)
    {
        syncing = true;
        try
        {
            foreach (var combo in combos)
            {
                if (combo.Id == comboId) continue;
                var keys = combo.Entry.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var kept = keys.Where(k => !spell.Contains(k, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (kept.Length != keys.Length) combo.Entry.Value = string.Join(",", kept);
            }
            var target = combos.FirstOrDefault(c => c.Id == comboId);
            if (target != null && !Lists(target.Entry.Value, spell)) target.Entry.Value = spell;
        }
        finally
        {
            syncing = false;
        }
    }

    private static void Sync()
    {
        syncing = true;
        try
        {
            foreach (var (spell, view) in views) view.Value = ComboOf(spell, c => c.Entry.Value);
        }
        finally
        {
            syncing = false;
        }
    }

    /// <summary>Minimap redraws its rows only when its own config changes; a combo taken from another card must show there too.</summary>
    private static void RefreshMinimap()
    {
        try
        {
            if (!IL2CPPChainloader.Instance.Plugins.TryGetValue(SettingsPage.MinimapGuid, out var info) || info.Instance == null) return;
            var options = info.Instance.GetType().GetField("Options", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(info.Instance);
            var revision = options?.GetType().GetProperty("Revision", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (revision != null) revision.SetValue(options, (int)revision.GetValue(options)! + 1);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogDebug($"Minimap rows not refreshed: {e.Message}");
        }
    }

    /// <summary>Asset name (<c>BloodSpikes_SpellData</c>) to display name; empty until the spells are loaded.</summary>
    private static List<KeyValuePair<string, string>> Names()
    {
        var result = new List<KeyValuePair<string, string>>();
        try
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SpellItemData>()))
            {
                var spell = o.TryCast<SpellItemData>();
                var name = spell?.itemDisplayName?.GetLocalizedString();
                if (spell != null && !string.IsNullOrWhiteSpace(name)) result.Add(new(spell.name, name));
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogDebug($"Card names unavailable: {e.Message}");
        }
        return result;
    }
}
