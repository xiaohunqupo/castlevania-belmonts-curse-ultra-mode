using HarmonyLib;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltraMode.Features;

/// <summary>
/// Story progress (the death-trap progression key set around The Hermit) locks fast travel and turns ordinary
/// teleport mirrors into unusable ones. The fast-travel lock is forced open and save rooms get their intact mirror
/// back; an ordinary mirror's own condition (the save room has been used) and shade/hostile mirrors keep their rules.
/// GPE_Mirror.CanUseMirror must not be patched: the function is too short for the native detour, and every mirror
/// taking its relocated branch crashed the game (int3 inside it).
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
    private static readonly List<(Transform hidden, List<Transform> shown)> changes = new();

    /// <summary>
    /// Rooms stream in as scenes; right after one loads every frame is scanned so the swap lands during the room
    /// transition instead of in view. Otherwise a slow scan catches mirrors switched by story events.
    /// </summary>
    internal static void Init()
    {
        ActionTracker.Updated += (e, _) => Update();
        Settings.OnChanged(() => { if (!Active) RestoreAll(); }, Settings.Enabled, Settings.RepairMirrors);
    }

    /// <summary>Swaps live in the loaded scenes, so turning the fix off has to undo them in place.</summary>
    private static void RestoreAll()
    {
        try
        {
            foreach (var (hidden, shown) in changes)
            {
                foreach (var t in shown)
                    if (t != null) t.gameObject.SetActive(false);
                if (hidden != null) hidden.gameObject.SetActive(true);
            }
            if (changes.Count > 0) Log.Debug($"MirrorRepair: {changes.Count} mirror swap(s) undone");
        }
        catch (Exception ex) { Fix.Fail(ex); }
        changes.Clear();
        swapped.Clear();
    }

    private static void Update()
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
    }

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
    /// Only save room mirrors are repaired. The Hall of Mirrors hub keeps several destination mirrors, some broken by
    /// design, in groups under one parent; swapping across those groups froze the game when the player came near.
    /// </summary>
    private static bool InSavePoint(GPE_Mirror m)
    {
        for (var t = m.transform; t != null; t = t.parent)
            if (t.name.Contains("SavePoint", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// After The Hermit, save rooms show a broken mirror (condition key ...BrokenMirror) while the original mirror
    /// stays in the scene, inactive. The branches holding them below their common ancestor are swapped back, but only
    /// when that ancestor holds no other mirror.
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
            swapped.Add(broken.Pointer);
            if (!InSavePoint(broken))
            {
                Log.Debug($"MirrorRepair: {broken.gameObject.scene.name}: '{Path(broken.transform)}' is not a save room mirror, left alone");
                continue;
            }
            var scene = broken.gameObject.scene.handle;
            GPE_Mirror? intact = null;
            Transform? brokenBranch = null, intactBranch = null;
            foreach (var m in mirrors)
            {
                if (m.gameObject.scene.handle != scene || m.gameObject.activeInHierarchy || IsBroken(m)) continue;
                var (b, i, common) = Branches(broken.transform, m.transform);
                if (common == null || b.Pointer == i.Pointer || common.GetComponentsInChildren<GPE_Mirror>(true).Length != 2) continue;
                (intact, brokenBranch, intactBranch) = (m, b, i);
                break;
            }
            if (intact == null || brokenBranch == null || intactBranch == null)
            {
                Log.Debug($"MirrorRepair: {broken.gameObject.scene.name}: no intact mirror paired with '{Path(broken.transform)}', not swapped");
                continue;
            }
            var shown = new List<Transform>();
            for (var t = intact.transform; t != null && t != intactBranch.parent; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(true);
                shown.Add(t);
            }
            brokenBranch.gameObject.SetActive(false);
            changes.Add((brokenBranch, shown));
            Log.Debug($"MirrorRepair: {broken.gameObject.scene.name}: hid '{Path(brokenBranch)}' ({ConditionKey(broken)}), " +
                      $"restored '{Path(intactBranch)}' ({ConditionKey(intact)})");
        }
    }

    /// <summary>The lowest common ancestor and its children that lead to each transform (roots if none).</summary>
    private static (Transform a, Transform b, Transform? common) Branches(Transform a, Transform b)
    {
        var ancestors = new HashSet<IntPtr>();
        for (var t = a; t != null; t = t.parent) ancestors.Add(t.Pointer);
        Transform? common = null;
        for (var t = b; t != null; t = t.parent)
            if (ancestors.Contains(t.Pointer)) { common = t; break; }
        return (ChildToward(common, a), ChildToward(common, b), common);
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
}
