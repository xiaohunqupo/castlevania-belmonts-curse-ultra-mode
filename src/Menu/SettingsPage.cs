using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Ui = UltraModeVendor.BelmontUi;

namespace UltraMode.Menu;

/// <summary>
/// Three MODS list entries: main Ultra Mode, Quick Actions, Spell Tweaks. Belmont Minimap discovers the first via
/// <see cref="BelmontModSettings.ProviderV2"/> and the other two via in-memory assemblies; Belmont UI registers all three.
/// </summary>
internal static class SettingsPage
{
    internal const string MinimapGuid = "local.belmont.minimap";
    internal const string BelmontUiGuid = "leandrobueno.belmontui";

    private const string ResetEn = "Restore defaults", ResetZh = "恢复默认";
    private const string ActionsResetHintEn = "D-pad swaps and Quick Cast back to defaults.", ActionsResetHintZh = "十字键换装换法术与快速施法恢复默认";
    private const string SpellResetHintEn = "Every spell tweak back to its default.", SpellResetHintZh = "所有法术优化恢复默认";

    internal static void Register(BasePlugin host)
    {
        var plugins = IL2CPPChainloader.Instance.Plugins;
        try
        {
            TarotPage.Init();
            if (plugins.ContainsKey(MinimapGuid))
            {
                BelmontModSettings.ExtraProviders.Emit();
                Plugin.Logger.LogInfo("Settings page: on Belmont Minimap's MODS tab (3 entries)");
                return;
            }
            if (plugins.TryGetValue(BelmontUiGuid, out var standalone) && standalone.Instance != null)
            {
                var asm = standalone.Instance.GetType().Assembly;
                Standalone.RegisterMain(asm);
                Standalone.RegisterSpells(asm);
                registerActions = () => Standalone.RegisterActions(asm);
                Plugin.Logger.LogInfo("Settings page: on the standalone Belmont UI's MODS tab");
            }
            else
            {
                Ui.BelmontUiPlugin.Start(host);
                Embedded.RegisterMain();
                Embedded.RegisterSpells();
                registerActions = Embedded.RegisterActions;
                Plugin.Logger.LogInfo("Settings page: on the embedded Belmont UI's MODS tab");
            }
            registerActions();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Settings page unavailable, settings are in the config file only.\n{e}");
        }
    }

    private static Action? registerActions;

    internal static void Update()
    {
        if (registerActions == null || Time.frameCount % 120 != 0 || !TarotPage.NamesLoaded) return;
        var again = registerActions;
        registerActions = null;
        try { again(); }
        catch (Exception e) { Plugin.Logger.LogError($"Quick Actions page not rebuilt.\n{e}"); }
    }

    private static class Embedded
    {
        internal static void RegisterMain()
        {
            Ui.Settings.Register(Plugin.Id, Text(SettingsRows.TitleEn, SettingsRows.TitleZh), menu =>
            {
                foreach (var row in SettingsRows.All())
                {
                    if (row.IsSlider)
                        menu.Slider(row.Entry, Text(row.LabelEn, row.LabelZh), row.Min, row.Max, row.Step, SettingsRows.SliderFormat,
                            Text(row.HintEn, row.HintZh));
                    else
                        menu.Toggle(row.Entry, Text(row.LabelEn, row.LabelZh), Text(row.HintEn, row.HintZh));
                }
                menu.ResetButton();
            }, Text(SettingsRows.SummaryEn, SettingsRows.SummaryZh));
        }

        internal static void RegisterActions()
        {
            Ui.Settings.Register(TarotPage.Id, Text(TarotPage.TitleEn, TarotPage.TitleZh), menu =>
            {
                menu.Toggle(Settings.QuickEquipWeapons, Text(TarotPage.WeaponsEn, TarotPage.WeaponsZh), Text(TarotPage.WeaponsHintEn, TarotPage.WeaponsHintZh));
                menu.Toggle(Settings.QuickEquipSpells, Text(TarotPage.SpellsEn, TarotPage.SpellsZh), Text(TarotPage.SpellsHintEn, TarotPage.SpellsHintZh));
                menu.Toggle(Settings.TarotShortcuts, Text(TarotPage.ToggleEn, TarotPage.ToggleZh), Text(TarotPage.ToggleHintEn, TarotPage.ToggleHintZh));
                var choices = TarotPage.ComboChoices();
                var labels = choices.LabelsEn.Zip(choices.LabelsZh, Text).ToArray();
                foreach (var row in TarotPage.Rows())
                    menu.Choice(row.Entry, Text(row.LabelEn, row.LabelZh), choices.Values, labels);
                menu.Button(Text(ResetEn, ResetZh), TarotPage.Reset, Text(ActionsResetHintEn, ActionsResetHintZh), Text("Reset", "重置"));
            }, Text(TarotPage.SummaryEn, TarotPage.SummaryZh));
        }

        internal static void RegisterSpells()
        {
            Ui.Settings.Register(SpellPage.Id, Text(SpellPage.TitleEn, SpellPage.TitleZh), menu =>
            {
                foreach (var row in SpellPage.Rows())
                    menu.Toggle(row.Entry, Text(row.LabelEn, row.LabelZh), Text(row.HintEn, row.HintZh));
                menu.Button(Text(ResetEn, ResetZh), SpellPage.Reset, Text(SpellResetHintEn, SpellResetHintZh), Text("Reset", "重置"));
            }, Text(SpellPage.SummaryEn, SpellPage.SummaryZh));
        }

        private static Ui.LocalizedText Text(string en, string zh) => new(en) { ["zh-CN"] = zh };
    }

    private static class Standalone
    {
        private static readonly object[] DefaultReset = { "Restore defaults", "Every setting of this mod back to its default.", "Reset" };

        internal static void RegisterMain(Assembly asm)
        {
            var (menu, textType, Text, toggle, slider, reset) = Open(asm, Plugin.Id, SettingsRows.TitleEn, SettingsRows.TitleZh,
                SettingsRows.SummaryEn, SettingsRows.SummaryZh);
            foreach (var row in SettingsRows.All())
            {
                if (row.IsSlider)
                    slider.Invoke(menu, new[] { row.Entry, Text(row.LabelEn, row.LabelZh), row.Min, row.Max, row.Step,
                        SettingsRows.SliderFormat, Text(row.HintEn, row.HintZh) });
                else
                    toggle.Invoke(menu, new[] { row.Entry, Text(row.LabelEn, row.LabelZh), Text(row.HintEn, row.HintZh) });
            }
            reset.Invoke(menu, DefaultReset);
        }

        internal static void RegisterActions(Assembly asm)
        {
            var (menu, textType, Text, toggle, _, _) = Open(asm, TarotPage.Id, TarotPage.TitleEn, TarotPage.TitleZh,
                TarotPage.SummaryEn, TarotPage.SummaryZh);
            var choice = Method(Need(asm, "BelmontUi.SettingsMenu"), "Choice", typeof(ConfigEntryBase), textType, typeof(string[]), textType.MakeArrayType(), textType);
            var button = Method(Need(asm, "BelmontUi.SettingsMenu"), "Button", textType, typeof(Action), textType, textType);
            toggle.Invoke(menu, new[] { Settings.QuickEquipWeapons, Text(TarotPage.WeaponsEn, TarotPage.WeaponsZh), Text(TarotPage.WeaponsHintEn, TarotPage.WeaponsHintZh) });
            toggle.Invoke(menu, new[] { Settings.QuickEquipSpells, Text(TarotPage.SpellsEn, TarotPage.SpellsZh), Text(TarotPage.SpellsHintEn, TarotPage.SpellsHintZh) });
            toggle.Invoke(menu, new[] { Settings.TarotShortcuts, Text(TarotPage.ToggleEn, TarotPage.ToggleZh), Text(TarotPage.ToggleHintEn, TarotPage.ToggleHintZh) });
            var choices = TarotPage.ComboChoices();
            var labels = Array.CreateInstance(textType, choices.Values.Length);
            for (var i = 0; i < labels.Length; i++) labels.SetValue(Text(choices.LabelsEn[i], choices.LabelsZh[i]), i);
            foreach (var row in TarotPage.Rows())
                choice.Invoke(menu, new[] { row.Entry, Text(row.LabelEn, row.LabelZh), choices.Values, labels, null });
            button.Invoke(menu, new[] { Text(ResetEn, ResetZh), (Action)TarotPage.Reset, Text(ActionsResetHintEn, ActionsResetHintZh), Text("Reset", "重置") });
        }

        internal static void RegisterSpells(Assembly asm)
        {
            var (menu, _, Text, toggle, _, _) = Open(asm, SpellPage.Id, SpellPage.TitleEn, SpellPage.TitleZh,
                SpellPage.SummaryEn, SpellPage.SummaryZh);
            var button = Method(Need(asm, "BelmontUi.SettingsMenu"), "Button", Need(asm, "BelmontUi.LocalizedText"), typeof(Action), Need(asm, "BelmontUi.LocalizedText"), Need(asm, "BelmontUi.LocalizedText"));
            foreach (var row in SpellPage.Rows())
                toggle.Invoke(menu, new[] { row.Entry, Text(row.LabelEn, row.LabelZh), Text(row.HintEn, row.HintZh) });
            button.Invoke(menu, new[] { Text(ResetEn, ResetZh), (Action)SpellPage.Reset, Text(SpellResetHintEn, SpellResetHintZh), Text("Reset", "重置") });
        }

        private static (object menu, Type textType, Func<string, string, object> Text, MethodInfo toggle, MethodInfo slider, MethodInfo reset)
            Open(Assembly asm, string id, string titleEn, string titleZh, string summaryEn, string summaryZh)
        {
            var settings = Need(asm, "BelmontUi.Settings");
            var menuType = Need(asm, "BelmontUi.SettingsMenu");
            var textType = Need(asm, "BelmontUi.LocalizedText");
            var indexer = textType.GetProperty("Item") ?? throw new MissingMemberException(textType.FullName, "Item");
            object Text(string en, string zh)
            {
                var text = Activator.CreateInstance(textType, en)!;
                indexer.SetValue(text, zh, new object[] { "zh-CN" });
                return text;
            }
            var register = Method(settings, "Register", typeof(string), textType, typeof(Action<>).MakeGenericType(menuType), textType);
            var menu = register.Invoke(null, new[] { id, Text(titleEn, titleZh), null, Text(summaryEn, summaryZh) })!;
            return (menu, textType, Text,
                Method(menuType, "Toggle", typeof(ConfigEntryBase), textType, textType),
                Method(menuType, "Slider", typeof(ConfigEntryBase), textType, typeof(float), typeof(float), typeof(float), typeof(string), textType),
                Method(menuType, "ResetButton", typeof(string), typeof(string), typeof(string)));
        }

        private static Type Need(Assembly asm, string name) =>
            asm.GetType(name) ?? throw new TypeLoadException($"{name} not found in {asm.GetName().Name}");

        private static MethodInfo Method(Type type, string name, params Type[] args) =>
            type.GetMethod(name, args) ?? throw new MissingMethodException(type.FullName, name);
    }
}
