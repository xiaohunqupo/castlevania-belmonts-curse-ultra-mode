using Il2CppInterop.Runtime.InteropTypes;
using MotherBase.ActionFramework;

namespace UltraMode;

internal static class ActionInfo
{
    private static readonly Dictionary<(IntPtr, Type), Il2CppObjectBase?> featureCache = new();

    internal static T? Feature<T>(ActionData? data) where T : ActionFeature
    {
        if (data == null) return null;
        var key = (data.Pointer, typeof(T));
        if (featureCache.TryGetValue(key, out var cached)) return (T?)cached;
        T? found = null;
        var features = data.actionFeatures;
        if (features != null)
            for (var i = 0; i < features.Count && found == null; i++)
                found = features[i]?.TryCast<T>();
        featureCache[key] = found;
        return found;
    }

    private static readonly Dictionary<IntPtr, bool> attackCache = new();

    internal static bool IsAttack(ActionData data)
    {
        if (!attackCache.TryGetValue(data.Pointer, out var result))
            attackCache[data.Pointer] = result = data.TryCast<WeaponActionData>() != null;
        return result;
    }
}
