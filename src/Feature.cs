using HarmonyLib;

namespace UltraMode;

/// <summary>Marks a Harmony patch class as part of a named feature; every class of a feature is installed or rolled back together.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class FeatureAttribute : Attribute
{
    internal readonly string Name;
    public FeatureAttribute(string name) => Name = name;
}

/// <summary>
/// A named group of patches with its own Harmony id, so it can be unpatched on its own. <see cref="Enabled"/> turns
/// false at the first failure and stays false until the game restarts; every patch and event handler checks it first.
/// </summary>
internal sealed class Feature
{
    private static readonly Dictionary<string, Feature> byName = new();
    internal static IEnumerable<Feature> All => byName.Values;

    internal readonly string Name;
    internal readonly Harmony Harmony;
    internal bool Enabled { get; private set; } = true;

    private Feature(string name)
    {
        Name = name;
        Harmony = new Harmony($"{Plugin.Id}.{name}");
    }

    internal static Feature Get(string name)
    {
        if (!byName.TryGetValue(name, out var feature))
            byName[name] = feature = new Feature(name);
        return feature;
    }

    /// <summary>Called from inside patches: exceptions must never escape into IL2CPP, so the feature just goes inert.</summary>
    internal void Fail(Exception error)
    {
        if (!Enabled) return;
        Enabled = false;
        try { Plugin.Logger.LogError($"[{Name}] disabled for this session; vanilla behaviour restored.\n{error}"); }
        catch { }
    }

    internal static void UnpatchAll()
    {
        foreach (var feature in All)
        {
            try { feature.Harmony.UnpatchSelf(); }
            catch (Exception e) { feature.Fail(e); }
        }
    }
}
