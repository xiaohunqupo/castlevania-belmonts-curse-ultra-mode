using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Ui = UltraModeVendor.BelmontUi;

namespace UltraMode.Menu;

/// <summary>
/// Puts <see cref="SettingsRows"/> on a pause-menu page: Belmont Minimap's MODS tab when it is installed (it finds
/// <see cref="BelmontModSettings.ProviderV2"/> itself), else a standalone Belmont UI's tab, else the copy of
/// Belmont UI embedded under vendor/BelmontUI. Only one of them is used, so the page never shows twice.
/// </summary>
internal static class SettingsPage
{
    internal const string MinimapGuid = "local.belmont.minimap";
    internal const string BelmontUiGuid = "leandrobueno.belmontui";

    internal static void Register(BasePlugin host)
    {
        var plugins = IL2CPPChainloader.Instance.Plugins;
        try
        {
            if (plugins.ContainsKey(MinimapGuid))
            {
                Plugin.Logger.LogInfo("Settings page: on Belmont Minimap's MODS tab");
                return;
            }
            if (plugins.TryGetValue(BelmontUiGuid, out var standalone) && standalone.Instance != null)
            {
                Standalone.Register(standalone.Instance.GetType().Assembly);
                Plugin.Logger.LogInfo("Settings page: on the standalone Belmont UI's MODS tab");
                return;
            }
            Embedded.Register(host);
            Plugin.Logger.LogInfo("Settings page: on the embedded Belmont UI's MODS tab");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Settings page unavailable, settings are in the config file only.\n{e}");
        }
    }

    private static class Embedded
    {
        internal static void Register(BasePlugin host)
        {
            Ui.BelmontUiPlugin.Start(host);
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

        private static Ui.LocalizedText Text(string en, string zh) => new(en) { ["zh-CN"] = zh };
    }

    /// <summary>The same page through reflection: this mod must load without the standalone BelmontUI.dll.</summary>
    private static class Standalone
    {
        // Upstream recognises these exact strings and shows its translated reset texts instead.
        private static readonly object[] DefaultReset = { "Restore defaults", "Every setting of this mod back to its default.", "Reset" };

        internal static void Register(Assembly asm)
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
            var toggle = Method(menuType, "Toggle", typeof(ConfigEntryBase), textType, textType);
            var slider = Method(menuType, "Slider", typeof(ConfigEntryBase), textType, typeof(float), typeof(float), typeof(float), typeof(string), textType);
            var reset = Method(menuType, "ResetButton", typeof(string), typeof(string), typeof(string));

            var menu = register.Invoke(null, new[] { Plugin.Id, Text(SettingsRows.TitleEn, SettingsRows.TitleZh), null,
                Text(SettingsRows.SummaryEn, SettingsRows.SummaryZh) });
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

        private static Type Need(Assembly asm, string name) =>
            asm.GetType(name) ?? throw new TypeLoadException($"{name} not found in {asm.GetName().Name}");

        private static MethodInfo Method(Type type, string name, params Type[] args) =>
            type.GetMethod(name, args) ?? throw new MissingMethodException(type.FullName, name);
    }
}
