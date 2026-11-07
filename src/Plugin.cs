using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Configuration;
using UnityEngine;

namespace UltraMode;

/// <summary>
/// Entry point. Every class tagged with <see cref="FeatureAttribute"/> is patched and its static <c>Init()</c> is
/// called; if either throws, only that feature is rolled back. <see cref="Version"/> must match the csproj.
/// </summary>
[BepInPlugin(Id, Name, Version)]
[BepInDependency(Menu.SettingsPage.MinimapGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(Menu.SettingsPage.BelmontUiGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "ultra_mode.belmontscurse";
    public const string Name = "Ultra Mode";
    public const string Version = "0.1.0";

    internal static ManualLogSource Logger = null!;
    internal static Plugin Instance = null!;

    public override void Load()
    {
        Instance = this;
        Logger = Log;
        Settings.Bind(Config);
        Config.SettingChanged += Settings.Dispatch;

        var groups = typeof(Plugin).Assembly.GetTypes()
            .Select(type => (type, feature: type.GetCustomAttribute<FeatureAttribute>()))
            .Where(x => x.feature != null)
            .GroupBy(x => x.feature!.Name, x => x.type);

        foreach (var group in groups)
        {
            var feature = Feature.Get(group.Key);
            try
            {
                foreach (var type in group)
                {
                    if (type.IsDefined(typeof(HarmonyLib.HarmonyPatch), false))
                        feature.Harmony.CreateClassProcessor(type).Patch();
                    type.GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(null, null);
                }
            }
            catch (Exception e)
            {
                feature.Fail(e);
                feature.Harmony.UnpatchSelf();
            }
        }

        AddComponent<UltraModeBehaviour>();
        Menu.SettingsPage.Register(this);

        var enabled = Feature.All.Where(f => f.Enabled).Select(f => f.Name).ToList();
        Log.LogInfo($"{Name} {Version} loaded; {enabled.Count}/{Feature.All.Count()} features active: {string.Join(", ", enabled)}. Toggle: {Hotkey(Settings.ToggleKeyEnabled, Settings.ToggleKey)}, dump: {Hotkey(Settings.DumpKeyEnabled, Settings.DumpKey)}.");
    }

    private static string Hotkey(ConfigEntry<bool> enabled, ConfigEntry<KeyboardShortcut> key) =>
        enabled.Value ? key.Value.ToString() : "off";

    public override bool Unload()
    {
        Feature.UnpatchAll();
        return true;
    }
}

/// <summary>Polls the hotkeys. An exception disables this component (the hotkeys stop working) instead of logging every frame.</summary>
internal sealed class UltraModeBehaviour : MonoBehaviour
{
    public UltraModeBehaviour(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            if (Settings.DumpKeyEnabled.Value && Settings.DumpKey.Value.IsDown()) Inspector.DumpAll();
            if (!Settings.ToggleKeyEnabled.Value || !Settings.ToggleKey.Value.IsDown()) return;
            Settings.Enabled.Value = !Settings.Enabled.Value;
            Plugin.Logger.LogMessage($"Ultra Mode {(Settings.Enabled.Value ? "ON" : "OFF")}");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError(e);
            enabled = false;
        }
    }
}
