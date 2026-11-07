using HarmonyLib;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltraMode.Features;

/// <summary>
/// Story progress (the death-trap progression key set around The Hermit) locks fast travel and turns ordinary
/// teleport mirrors into unusable ones. The fast-travel lock and the broken mirror's condition are forced open; an
/// ordinary mirror's own condition (the save room has been used) and shade/hostile mirrors keep their rules.
/// </summary>
[Feature("MirrorRepair")]
[HarmonyPatch]
internal static class MirrorRepair
{
    private static readonly Feature Fix = Feature.Get("MirrorRepair");
    private static readonly HashSet<IntPtr> reported = new();

    private static bool Active => Fix.Enabled && Settings.On && Settings.RepairMirrors.Value;

    private const int ScanInterval = 60;
    private const int BurstFrames = 120;
    private static int nextScan, burstUntil, sceneSignature;
    private static readonly HashSet<IntPtr> swapped = new();

    /// <summary>
    /// Rooms stream in as scenes; right after one loads every frame is scanned so the swap lands during the room
    /// transition instead of in view. Otherwise a slow scan catches mirrors switched by story events.
    /// </summary>
    internal static void Init() => ActionTracker.Updated += (e, _) =>
    {
        if (!Active) return;
        try
        {
            var signature = SceneSignature();
            if (signature != sceneSignature)
            {
                sceneSignature = signature;
                burstUntil = Time.frameCount + BurstFrames;
            }
            if (Time.frameCount > burstUntil && Time.frameCount < nextScan) return;
            nextScan = Time.frameCount + ScanInterval;
            SwapBrokenMirrors();
        }
        catch (Exception ex) { Fix.Fail(ex); }
    };

    private static int SceneSignature()
    {
        var hash = SceneManager.sceneCount;
        for (var i = 0; i < SceneManager.sceneCount; i++)
            hash = hash * 31 + SceneManager.GetSceneAt(i).handle;
        return hash;
    }

    private static string? ConditionKey(GPE_Mirror m) =>
        m.useMirrorCondition?.TryCast<EntityConditionCheckProgressKey>()?.progressionKeyToCheck?.name;

    private static bool IsBroken(GPE_Mirror m) => ConditionKey(m)?.Contains("Broken", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// After The Hermit, save rooms show a broken mirror (condition key ...BrokenMirror) while the original mirror
    /// stays in the scene, inactive. The branches holding them below their common ancestor are swapped back.
    /// </summary>
    private static void SwapBrokenMirrors()
    {
        var mirrors = new List<GPE_Mirror>();
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GPE_Mirror>()))
        {
            var m = o.TryCast<GPE_Mirror>();
            if (m != null && m.gameObject.scene.IsValid() && m.mirrorType == MirrorType.BaseMirror) mirrors.Add(m);
        }
        foreach (var broken in mirrors)
        {
            if (!broken.gameObject.activeInHierarchy || swapped.Contains(broken.Pointer) || !IsBroken(broken)) continue;
            var scene = broken.gameObject.scene.handle;
            var intact = mirrors.FirstOrDefault(m => m.gameObject.scene.handle == scene && !m.gameObject.activeInHierarchy && !IsBroken(m));
            if (intact == null) continue;
            swapped.Add(broken.Pointer);

            var (brokenBranch, intactBranch) = Branches(broken.transform, intact.transform);
            if (brokenBranch.Pointer == intactBranch.Pointer)
            {
                Log.Debug($"MirrorRepair: {broken.gameObject.scene.name}: mirrors share a branch, not swapped");
                continue;
            }
            for (var t = intact.transform; t != null && t != intactBranch.parent; t = t.parent)
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            brokenBranch.gameObject.SetActive(false);
            Log.Debug($"MirrorRepair: {broken.gameObject.scene.name}: hid '{Path(brokenBranch)}' ({ConditionKey(broken)}), " +
                      $"restored '{Path(intactBranch)}' ({ConditionKey(intact)})");
        }
    }

    /// <summary>The children of the lowest common ancestor that lead to each transform (roots if none).</summary>
    private static (Transform a, Transform b) Branches(Transform a, Transform b)
    {
        var ancestors = new HashSet<IntPtr>();
        for (var t = a; t != null; t = t.parent) ancestors.Add(t.Pointer);
        Transform? common = null;
        for (var t = b; t != null; t = t.parent)
            if (ancestors.Contains(t.Pointer)) { common = t; break; }
        return (ChildToward(common, a), ChildToward(common, b));
    }

    private static Transform ChildToward(Transform? ancestor, Transform t)
    {
        while (t.parent != null && (ancestor == null || t.parent.Pointer != ancestor.Pointer)) t = t.parent;
        return t;
    }

    private static string Path(Transform t) => t.parent == null ? t.name : $"{Path(t.parent)}/{t.name}";

    [HarmonyPatch(typeof(FastTravelMenuManager), nameof(FastTravelMenuManager.IsDeathTrapActive)), HarmonyPostfix]
    private static void IsDeathTrapActive(FastTravelMenuManager __instance, ref bool __result)
    {
        if (!__result || !Active) return;
        try
        {
            if (reported.Add(__instance.Pointer))
                Log.Debug($"MirrorRepair: fast travel lock '{__instance.deathTrapProgressionKey?.name}' ignored");
            __result = false;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(GPE_Mirror), nameof(GPE_Mirror.CanUseMirror)), HarmonyPostfix]
    private static void CanUseMirror(GPE_Mirror __instance, Entity mc, ref bool __result)
    {
        if (__result || !Active) return;
        try
        {
            if (__instance.mirrorType != MirrorType.BaseMirror || !IsBroken(__instance)) return;
            if (reported.Add(__instance.Pointer))
                Log.Debug($"MirrorRepair: mirror {__instance.name} ({__instance.gameObject.scene.name}) repaired, condition={__instance.useMirrorCondition?.GetIl2CppType().Name}");
            __result = true;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
