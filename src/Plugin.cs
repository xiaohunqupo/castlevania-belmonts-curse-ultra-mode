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
[BepInDependency(ConfigManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "ultra_mode.belmontscurse";
    public const string Name = "Ultra Mode";
    public const string Version = "0.2.0";
    /// <summary>BepInEx Config Manager (Nexus). Its patcher/UniverseLib breaks MonoMod hooks during our Load.</summary>
    public const string ConfigManagerGuid = "com.sinai.BepInExConfigManager";

    internal static ManualLogSource Logger = null!;
    internal static Plugin Instance = null!;

    public override void Load()
    {
        Instance = this;
        Logger = Log;

        // Detect before any Harmony / Il2CppInterop registration. Config Manager's patcher leaves MonoMod's
        // CompileMethodHook in a state where our Harmony.Patch (and sometimes AddComponent) fatals the CLR.
        Settings.ConfigManagerConflict = DetectConfigManager();

        Settings.Bind(Config);
        Config.SettingChanged += Settings.Dispatch;

        if (Settings.ConfigManagerConflict)
        {
            if (Settings.Enabled.Value) Settings.Enabled.Value = false;
            Settings.OnChanged(RejectEnableWhileConflict, Settings.Enabled);
            Log.LogWarning($"{Name}: BepInEx Config Manager detected. Harmony patches are skipped and Ultra Mode stays off to avoid a startup crash. Remove Config Manager and restart to use Ultra Mode.");
            // Settings page only — no AddComponent (Il2Cpp type registration) and no Harmony.
            try { Menu.SettingsPage.Register(this); }
            catch (Exception e) { Log.LogError(e); }
            Log.LogInfo($"{Name} {Version} loaded inert (Config Manager conflict).");
            return;
        }

        ApplyPatches();
        AddComponent<UltraModeBehaviour>();
        Menu.SettingsPage.Register(this);

        var enabled = Feature.All.Where(f => f.Enabled).Select(f => f.Name).ToList();
        Log.LogInfo($"{Name} {Version} loaded; {enabled.Count}/{Feature.All.Count()} features active: {string.Join(", ", enabled)}. Toggle: {Hotkey(Settings.ToggleKeyEnabled, Settings.ToggleKey)}, dump: {Hotkey(Settings.DumpKeyEnabled, Settings.DumpKey)}.");
    }

    private static bool DetectConfigManager()
    {
        try
        {
            if (IL2CPPChainloader.Instance?.Plugins != null &&
                IL2CPPChainloader.Instance.Plugins.ContainsKey(ConfigManagerGuid))
                return true;
        }
        catch { /* fall through to file checks */ }

        try
        {
            var root = Paths.BepInExRootPath;
            if (File.Exists(Path.Combine(root, "patchers", "BepInExConfigManager.Il2Cpp.Patcher.dll")))
                return true;
            if (File.Exists(Path.Combine(root, "plugins", "BepInExConfigManager", "BepInExConfigManager.Il2Cpp.CoreCLR.dll")))
                return true;
        }
        catch { }

        return false;
    }

    private void ApplyPatches()
    {
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
    }

    private static void RejectEnableWhileConflict()
    {
        if (!Settings.ConfigManagerConflict || !Settings.Enabled.Value) return;
        Settings.Enabled.Value = false;
        Logger.LogWarning($"{Name}: cannot enable while BepInEx Config Manager is installed. Remove it and restart.");
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
        Menu.ScrollFollow.Update();
        Menu.SettingsPage.Update();
        try
        {
            Features.TarotShortcuts.PollInput();
            Features.QuickEquip.PollInput();
            Features.TarotHint.Tick();
            if (Settings.DumpKeyEnabled.Value && Settings.DumpKey.Value.IsDown()) Inspector.DumpAll();
            if (!Settings.ToggleKeyEnabled.Value || !Settings.ToggleKey.Value.IsDown()) return;
            if (Settings.ConfigManagerConflict)
            {
                Plugin.Logger.LogWarning("Ultra Mode cannot be toggled while BepInEx Config Manager is installed.");
                return;
            }
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
