using HarmonyLib;
using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// Anchor highlights come from DistanceReactionSystem, which only measures distance. Each highlighted anchor is probed
/// with the game's own target search (FindEntityTarget) aimed straight at it, so walls, range and target rules all
/// match the real hook. The result drives three things: unreachable anchors lose their highlight, a lone reachable
/// anchor is hooked whatever the aim, and a press with nothing to hook does not start the whip at all.
/// </summary>
[Feature("WhipAssist")]
[HarmonyPatch]
internal static class WhipAssist
{
    private static readonly Feature Fix = Feature.Get("WhipAssist");
    private const int ScanInterval = 60;

    private static bool Active => Fix.Enabled && Settings.On;

    private sealed class Anchor
    {
        internal readonly WhipDistanceComponent Distance;
        internal readonly Entity Entity;
        internal readonly Transform Point;
        /// <summary>The distance system wants it lit (between its AddFeedback and RemoveFeedback calls).</summary>
        internal bool Wanted;
        /// <summary>We are holding its highlight back because it is out of reach.</summary>
        internal bool Hidden;
        internal Anchor(WhipDistanceComponent distance, Entity entity, Transform point) { Distance = distance; Entity = entity; Point = point; Wanted = distance.activated; }
    }

    private static readonly List<Anchor> anchors = new();
    private static readonly List<Anchor> reachable = new();
    private static int nextScan, evaluatedFrame = -1;
    private static IntPtr detectFor;
    private static WhipHookDetect? probeDetect;

    /// <summary>What GetAimDirection returns while <see cref="Probe"/> runs the game's search.</summary>
    private static Vector3? aimOverride;
    /// <summary>The anchor auto-aim picked for the whip that is starting.</summary>
    private static Anchor? forced;
    private static bool whipRunning;
    private static int whipStartFrame;
    /// <summary>We are calling Add/RemoveFeedback ourselves, so our prefixes on them let the call through.</summary>
    private static bool selfCall;
    private static string lastDecision = "";
    private static readonly HashSet<IntPtr> mismatchReported = new();

    internal static void Init()
    {
        ActionTracker.Updated += (e, _) =>
        {
            if (!Fix.Enabled) return;
            try { Update(e); }
            catch (Exception ex) { Fix.Fail(ex); }
        };
        Settings.OnChanged(RestoreHighlights, Settings.Enabled, Settings.WhipHighlightReachable);
    }

    private static void Update(Entity e)
    {
        if (whipRunning && Time.frameCount > whipStartFrame + 2 && !ActionTracker.IsActive(d => ActionInfo.Feature<WhipHookDetect>(d) != null))
        {
            Log.Debug("WhipAssist: whip ended without OnStop");
            whipRunning = false;
            forced = null;
        }
        if (Time.frameCount >= nextScan)
        {
            nextScan = Time.frameCount + ScanInterval;
            Scan();
        }
        if (detectFor != e.Pointer)
        {
            detectFor = e.Pointer;
            probeDetect = FindDetect();
        }
        if (!Settings.On) return;
        Evaluate(e);
        if (Settings.WhipHighlightReachable.Value) SyncHighlights();
    }

    private static void Scan()
    {
        var seen = new HashSet<IntPtr>(anchors.Select(a => a.Distance.Pointer));
        anchors.RemoveAll(a => a.Distance == null || a.Distance.WasCollected || a.Entity == null || a.Entity.WasCollected);
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<WhipDistanceComponent>()))
        {
            var d = o.TryCast<WhipDistanceComponent>();
            if (d == null || seen.Contains(d.Pointer)) continue;
            var entity = d.GetComponent<Entity>();
            if (entity == null) entity = d.GetComponentInParent<Entity>();
            if (entity == null) continue;
            var whipable = Player.Comp<Whipable>(entity);
            anchors.Add(new Anchor(d, entity, whipable != null ? whipable.transform : d.transform));
        }
    }

    /// <summary>The player's whip action carries the detection settings; any instance works for probing.</summary>
    private static WhipHookDetect? FindDetect()
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<ActionData>()))
        {
            var data = o.TryCast<ActionData>();
            if (data == null || !data.name.StartsWith("MC_", StringComparison.Ordinal)) continue;
            var detect = ActionInfo.Feature<WhipHookDetect>(data);
            if (detect == null) continue;
            Log.Debug($"WhipAssist: probing with {data.name}");
            return detect;
        }
        Log.Debug("WhipAssist: no WhipHookDetect found");
        return null;
    }

    /// <summary>Highlighted anchors the hook can actually reach, refreshed once per frame and frozen while a whip runs.</summary>
    private static void Evaluate(Entity e)
    {
        if (evaluatedFrame == Time.frameCount || whipRunning) return;
        evaluatedFrame = Time.frameCount;
        reachable.Clear();
        var state = Player.Comp<WhipHookState>(e);
        if (probeDetect == null || state == null) return;
        foreach (var a in anchors)
        {
            if (a.Distance == null || a.Distance.WasCollected || !(a.Wanted || a.Distance.activated) || !a.Entity.gameObject.activeInHierarchy) continue;
            var origin = probeDetect.GetWhipOriginPoint(e);
            if (!Probe(probeDetect, e, state, (a.Point.position - origin).normalized, out var target) || target == null) continue;
            if (target.Pointer == a.Entity.Pointer) reachable.Add(a);
            else if (mismatchReported.Add(a.Entity.Pointer))
                Log.Debug($"WhipAssist: aiming at {a.Entity.name} found {target.name} instead");
        }
    }

    /// <summary>Runs the game's target search without leaving any trace on the hook state.</summary>
    private static bool Probe(WhipHookDetect detect, Entity e, WhipHookState state, Vector3? aim, out Entity? target)
    {
        var saved = (state.aimDirection, state.whipTargetType, state.hookTarget, state.hookTargetWhipable, state.lastHookTarget, state.whipHitPosition);
        var savedOverride = aimOverride;
        aimOverride = aim;
        if (aim is { } dir) state.aimDirection = dir;
        try
        {
            var ok = detect.FindEntityTarget(e, state, out var found);
            target = found;
            return ok;
        }
        finally
        {
            aimOverride = savedOverride;
            (state.aimDirection, state.whipTargetType, state.hookTarget, state.hookTargetWhipable, state.lastHookTarget, state.whipHitPosition) = saved;
        }
    }

    private static void SyncHighlights()
    {
        foreach (var a in anchors)
        {
            if (a.Distance == null || a.Distance.WasCollected) continue;
            var show = reachable.Contains(a);
            if (a.Wanted && !a.Hidden && !show)
            {
                SelfCall(() => a.Distance.RemoveFeedback(a.Entity));
                a.Hidden = true;
            }
            else if (a.Hidden && show)
            {
                a.Hidden = false;
                if (a.Distance.CanPlayFeedback(a.Entity)) SelfCall(() => a.Distance.AddFeedback(a.Entity));
            }
        }
    }

    private static void SelfCall(Action call)
    {
        selfCall = true;
        try { call(); }
        finally { selfCall = false; }
    }

    private static void RestoreHighlights()
    {
        try
        {
            foreach (var a in anchors)
            {
                if (!a.Hidden || a.Distance == null || a.Distance.WasCollected) continue;
                a.Hidden = false;
                if (a.Wanted && a.Distance.CanPlayFeedback(a.Entity)) SelfCall(() => a.Distance.AddFeedback(a.Entity));
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    /// <summary>A highlight is played by the distance system on entry; it stays off while we judge the anchor unreachable.</summary>
    [HarmonyPatch(typeof(WhipDistanceComponent), nameof(WhipDistanceComponent.AddFeedback)), HarmonyPrefix]
    private static bool AddFeedback(WhipDistanceComponent __instance)
    {
        if (selfCall) return true;
        try
        {
            var a = Find(__instance);
            if (a == null) return true;
            a.Wanted = true;
            if (!Active || !Settings.WhipHighlightReachable.Value || reachable.Contains(a)) return true;
            a.Hidden = true;
            return false;
        }
        catch (Exception ex) { Fix.Fail(ex); return true; }
    }

    /// <summary>Leaving range ends the wish; a highlight we held back has nothing to remove.</summary>
    [HarmonyPatch(typeof(WhipDistanceComponent), nameof(WhipDistanceComponent.RemoveFeedback)), HarmonyPrefix]
    private static bool RemoveFeedback(WhipDistanceComponent __instance)
    {
        if (selfCall) return true;
        try
        {
            var a = Find(__instance);
            if (a == null) return true;
            a.Wanted = false;
            if (!a.Hidden) return true;
            a.Hidden = false;
            return false;
        }
        catch (Exception ex) { Fix.Fail(ex); return true; }
    }

    private static Anchor? Find(WhipDistanceComponent d) => anchors.FirstOrDefault(x => x.Distance != null && !x.Distance.WasCollected && x.Distance.Pointer == d.Pointer);

    /// <summary>
    /// A search that succeeds without an entity is a miss. The lone anchor is forced even when the current aim already
    /// finds it: the real search runs frames later (detectionStartFrame) and the aim may have drifted off it by then.
    /// An anchor the current aim finds but the aimed probe does not is at the edge of reach and missed by the real
    /// search, so it counts as nothing, matching its missing highlight.
    /// </summary>
    [HarmonyPatch(typeof(WhipHookDetect), nameof(WhipHookDetect.CanStart)), HarmonyPrefix]
    private static bool CanStart(WhipHookDetect __instance, Entity e, ref bool __result)
    {
        if (!Active || whipRunning || !Player.Is(e)) return true;
        try
        {
            forced = null;
            var state = Player.Comp<WhipHookState>(e);
            if (state == null) return true;
            var hasNatural = Probe(__instance, e, state, null, out var natural) && natural != null;
            Evaluate(e);
            if (hasNatural && anchors.Any(a => a.Entity.Pointer == natural!.Pointer && !reachable.Contains(a)))
            {
                Log.Debug($"WhipAssist: {natural!.name} found only at the edge of reach, ignored");
                hasNatural = false;
            }
            var lone = Settings.WhipAutoAim.Value && reachable.Count == 1 ? reachable[0] : null;
            if (hasNatural && (lone == null || natural!.Pointer != lone.Entity.Pointer)) return Decide($"normal ({natural!.name})", true);
            if (lone != null)
            {
                forced = lone;
                return Decide("auto-aim at " + lone.Entity.name, true);
            }
            if (!Settings.WhipSkipEmpty.Value) return true;
            __result = false;
            return Decide("nothing to hook, blocked", false);
        }
        catch (Exception ex) { Fix.Fail(ex); return true; }
    }

    private static bool Decide(string decision, bool runOriginal)
    {
        if (decision != lastDecision)
        {
            lastDecision = decision;
            Log.Debug($"WhipAssist: {decision} (reachable={reachable.Count}/{anchors.Count(a => a.Wanted)})");
        }
        return runOriginal;
    }

    [HarmonyPatch(typeof(WhipHookDetect), nameof(WhipHookDetect.GetAimDirection)), HarmonyPostfix]
    private static void GetAimDirection(WhipHookDetect __instance, Entity e, ref Vector3 __result)
    {
        if (!Active) return;
        try
        {
            if (aimOverride is { } dir) { __result = dir; return; }
            if (forced == null || !Player.Is(e)) return;
            if (forced.Point == null || forced.Point.WasCollected) { forced = null; return; }
            __result = (forced.Point.position - __instance.GetWhipOriginPoint(e)).normalized;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(WhipHookDetect), nameof(WhipHookDetect.OnStart)), HarmonyPostfix]
    private static void OnStart(Entity e)
    {
        if (!Player.Is(e)) return;
        whipRunning = true;
        whipStartFrame = Time.frameCount;
    }

    [HarmonyPatch(typeof(WhipHookDetect), nameof(WhipHookDetect.OnStop)), HarmonyPostfix]
    private static void OnStop(Entity e)
    {
        if (!Player.Is(e)) return;
        whipRunning = false;
        forced = null;
    }
}
