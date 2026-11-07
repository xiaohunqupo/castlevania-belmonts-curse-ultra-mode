using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace UltraModeVendor.BelmontUi;

/// <summary>
/// Stands in for upstream Plugin.cs, which tools/sync-belmontui.ps1 leaves out: the embedded copy must not be a
/// BepInEx plugin of its own (it would take the standalone Belmont UI's GUID), so the host mod starts it.
/// </summary>
public static class BelmontUiPlugin
{
    public const string Version = "0.4.0";

    internal static ManualLogSource Logger = null!;

    internal static void Start(BasePlugin host)
    {
        Logger = BepInEx.Logging.Logger.CreateLogSource("Belmont UI (Ultra Mode)");
        host.AddComponent<BelmontUiWatcher>();
        Logger.LogInfo($"Belmont UI {Version} embedded in Ultra Mode started");
    }
}
