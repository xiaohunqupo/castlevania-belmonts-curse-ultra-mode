using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// Compresses action timing in data: every frame-number field of an action and its features is remapped from the
/// cached original, so settings changes and the master toggle can reapply or restore at any time.
/// Frames up to the first hit use the wind-up speed, later frames the recovery speed.
/// </summary>
internal static class FrameScaler
{
    private static readonly string[] excluded = { "hitstop", "invulnerab", "slowmo", "version", "recoverinvul" };

    internal sealed class FrameField
    {
        internal readonly Il2CppObjectBase Target;
        internal readonly PropertyInfo Prop;
        internal readonly int Original;
        internal FrameField(Il2CppObjectBase target, PropertyInfo prop, int original) { Target = target; Prop = prop; Original = original; }
    }

    private sealed class Plan
    {
        internal readonly ActionData Data;
        internal readonly List<FrameField> Fields = new();
        internal int OriginalEnd, FirstHit;
        internal float OriginalTimeScale;
        internal bool ScaleOpenEnded;
        internal Func<(float windup, float recovery)> Speeds = () => (1f, 1f);
        internal Func<FrameField, int?>? Pinned;
        internal Plan(ActionData data) => Data = data;
    }

    private static readonly Dictionary<IntPtr, Plan> plans = new();
    private static readonly HashSet<IntPtr> claimed = new();

    internal static bool Has(ActionData data) => plans.ContainsKey(data.Pointer);

    /// <param name="scaleOpenEnded">Actions without an end frame get their animation sped up directly.</param>
    /// <param name="pinned">Returns a fixed frame for fields that should not be scaled.</param>
    internal static void Register(ActionData data, Func<(float windup, float recovery)> speeds, bool scaleOpenEnded = false, Func<FrameField, int?>? pinned = null)
    {
        if (plans.ContainsKey(data.Pointer)) return;
        var plan = new Plan(data) { Speeds = speeds, OriginalEnd = data.endFrame, OriginalTimeScale = data.animTimeScale, ScaleOpenEnded = scaleOpenEnded, Pinned = pinned };
        var weapon = data.TryCast<WeaponActionData>();
        plan.FirstHit = weapon != null ? Math.Max(weapon.firstHitFrame, 0) : 0;

        Collect(plan, weapon != null ? weapon : data, 1);
        var features = data.actionFeatures;
        if (features != null)
            for (var i = 0; i < features.Count; i++)
                if (features[i] != null) Collect(plan, features[i], 1);

        plans[data.Pointer] = plan;
        Apply(plan);
        Log.Debug($"FrameScaler: {data.name} fields={plan.Fields.Count} end={plan.OriginalEnd} firstHit={plan.FirstHit}");
    }

    private static void Collect(Plan plan, Il2CppObjectBase target, int depth)
    {
        if (!claimed.Add(target.Pointer)) return;
        var obj = (Il2CppObjectBase)Inspector.Resolve(target);
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
            var lower = p.Name.ToLowerInvariant();
            if (p.PropertyType == typeof(int) && p.CanWrite && (lower.Contains("frame") || lower is "endframe" or "fadeinduration" or "fadeoutduration"))
            {
                if (excluded.Any(lower.Contains)) continue;
                try { plan.Fields.Add(new FrameField(obj, p, (int)p.GetValue(obj)!)); } catch { }
            }
            else if (depth > 0 && p.PropertyType.Name.StartsWith("List`1"))
            {
                try
                {
                    if (p.GetValue(obj) is not Il2CppObjectBase list) continue;
                    var count = (int)list.GetType().GetProperty("Count")!.GetValue(list)!;
                    var item = list.GetType().GetProperty("Item")!;
                    for (var i = 0; i < count; i++)
                        if (item.GetValue(list, new object[] { i }) is Il2CppObjectBase element and not UnityEngine.Object and not Il2CppSystem.ValueType)
                            Collect(plan, element, depth - 1);
                }
                catch { }
            }
        }
    }

    private static int Map(Plan plan, int frame, float windup, float recovery)
    {
        if (frame <= 0) return frame;
        var h = plan.FirstHit;
        var mapped = h > 0 && frame > h ? h / windup + (frame - h) / recovery : frame / windup;
        return Math.Max(1, Mathf.RoundToInt(mapped));
    }

    private static void Apply(Plan plan)
    {
        var (w, r) = Settings.On ? plan.Speeds() : (1f, 1f);
        foreach (var f in plan.Fields)
        {
            var frame = Settings.On && f.Original > 0 && plan.Pinned?.Invoke(f) is { } pin ? pin : Map(plan, f.Original, w, r);
            try { f.Prop.SetValue(f.Target, frame); } catch { }
        }
        if (plan.Data.scaleMode != ActionData.ScaleMode.EndFrame && plan.OriginalEnd > 0)
        {
            var newEnd = Map(plan, plan.OriginalEnd, w, r);
            plan.Data.animTimeScale = plan.OriginalTimeScale * plan.OriginalEnd / newEnd;
        }
        else if (plan.ScaleOpenEnded && plan.OriginalEnd <= 0)
            plan.Data.animTimeScale = plan.OriginalTimeScale * w;
    }

    internal static void ReapplyAll()
    {
        foreach (var plan in plans.Values)
        {
            if (plan.Data == null || plan.Data.WasCollected) continue;
            Apply(plan);
        }
    }
}

[Feature("FastResponse")]
internal static class FastResponse
{
    private static readonly Feature Fix = Feature.Get("FastResponse");
    private static float[]? appliedSpeeds;
    private static float appliedAccel = float.NaN, appliedDecel = float.NaN;
    private static IntPtr scannedMovementFeature;

    internal static void Init()
    {
        ActionTracker.Started += OnStarted;
        ActionTracker.Updated += OnUpdated;
        Settings.OnChanged(FrameScaler.ReapplyAll, Settings.Enabled, Settings.AttackWindupSpeed, Settings.AttackRecoverySpeed, Settings.TurnStopSpeed, Settings.WhipSpeed, Settings.MirrorSpeed, Settings.InstantMirror);
    }

    private static (float, float) MirrorSpeeds() => (Settings.MirrorSpeed.Value, Settings.MirrorSpeed.Value);

    private static IntPtr mirrorScanFor;
    private static bool mirrorBaseFound, mirrorRescanned;

    private const string MirrorEnterAction = "MC_Crouch_MirrorInteract_Tp";
    private const string CrouchAction = "MC_Crouch";
    private const string CrouchStartAction = "MC_Crouch_Start";

    /// <summary>The stick-down hold, the mirror interaction and the control lock that follows it.</summary>
    private static int? InstantMirrorField(FrameScaler.FrameField f)
    {
        if (!Settings.InstantMirror.Value) return null;
        return (f.Target, f.Prop.Name) switch
        {
            (InputConditionFeature, "maxFrameBeforeStop") => 1,
            (InteractWithCurrentMirror, "atFrame") => 1,
            (LockControlsFeature, "startFrame") => 1,
            _ => null,
        };
    }

    /// <summary>Crouching only checks for a mirror under the player in a one-frame window, 30 frames in.</summary>
    private static int? InstantMirrorCheck(FrameScaler.FrameField f)
    {
        if (!Settings.InstantMirror.Value || f.Target is not TryStartActionOverTimeFeature) return null;
        return f.Prop.Name switch
        {
            "startFrame" => 1,
            "endFrame" => 2,
            _ => null,
        };
    }

    private static void RegisterMirror(ActionData data) =>
        FrameScaler.Register(data, MirrorSpeeds, scaleOpenEnded: true, pinned: data.name == MirrorEnterAction ? InstantMirrorField : null);

    /// <summary>Only the mirror check windows move; everything else in the crouch stays as it is.</summary>
    private static void RegisterCrouch(ActionData data)
    {
        if (data.name == CrouchAction) FrameScaler.Register(data, () => (1f, 1f), pinned: InstantMirrorCheck);
        else if (data.name == CrouchStartAction) FrameScaler.Register(data, TurnSpeeds);
    }

    private static bool IsMirrorAction(string name) =>
        name.Contains("MirrorInteract", StringComparison.Ordinal) || name.StartsWith("GPE_MirrorBase_", StringComparison.Ordinal);

    /// <summary>
    /// A mirror is entered once, so waiting for first use is too late: the hold stage would already be running.
    /// One scan when a player entity appears, and one more on the first mirror action if the mirror's own actions
    /// were not loaded yet.
    /// </summary>
    private static void RegisterMirrorActions()
    {
        var count = 0;
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<ActionData>()))
        {
            var data = o.TryCast<ActionData>();
            if (data == null) continue;
            if (data.name is CrouchAction or CrouchStartAction) { RegisterCrouch(data); continue; }
            if (!IsMirrorAction(data.name)) continue;
            if (data.name.StartsWith("GPE_", StringComparison.Ordinal)) mirrorBaseFound = true;
            RegisterMirror(data);
            count++;
        }
        Log.Debug($"FastResponse: {count} mirror actions registered (mirror base found={mirrorBaseFound})");
    }

    private static (float, float) AttackSpeeds() => (Settings.AttackWindupSpeed.Value, Settings.AttackRecoverySpeed.Value);
    private static (float, float) TurnSpeeds() => (Settings.TurnStopSpeed.Value, Settings.TurnStopSpeed.Value);
    private static (float, float) WhipSpeeds() => (Settings.WhipSpeed.Value, Settings.WhipSpeed.Value);

    private static void OnStarted(Entity e, ActionData data)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (ActionInfo.IsAttack(data)) FrameScaler.Register(data, AttackSpeeds);
            else if (data.endFrame > 0 && data.name.StartsWith("MC_Whip", StringComparison.Ordinal)) FrameScaler.Register(data, WhipSpeeds);
            else if (data.name is CrouchAction or CrouchStartAction) RegisterCrouch(data);
            else if (IsMirrorAction(data.name))
            {
                if (!mirrorBaseFound && !mirrorRescanned)
                {
                    mirrorRescanned = true;
                    RegisterMirrorActions();
                }
                RegisterMirror(data);
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (mirrorScanFor != e.Pointer)
            {
                mirrorScanFor = e.Pointer;
                RegisterMirrorActions();
            }
            ScanMovementReactions(e);
            TuneMovement(e);
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    /// <summary>Turn-around, stop and start are separate reaction actions triggered by the player's movement feature.</summary>
    private static void ScanMovementReactions(Entity e)
    {
        foreach (var data in ActionTracker.Active.Values)
        {
            var move = ActionInfo.Feature<McMovementFeature>(data);
            if (move == null || move.Pointer == scannedMovementFeature) continue;
            scannedMovementFeature = move.Pointer;
            foreach (var trigger in new[] { move.volteReaction, move.volteAccelerationReaction, move.accelerationReaction, move.decelerationReaction, move.closeWallReaction })
            {
                var reaction = trigger?.GetActionData(e);
                if (reaction != null) FrameScaler.Register(reaction, TurnSpeeds);
            }
        }
    }

    /// <summary>Only values still at their data default (or our last write) are retargeted, so slows and water effects keep working.</summary>
    private static void TuneMovement(Entity e)
    {
        var state = Player.Comp<CharacterMovementState>(e);
        var data = state != null ? state.characterMovementData : null;
        if (state == null || data == null) return;

        var on = Settings.On;
        var speedMult = on ? Settings.MoveSpeed.Value : 1f;
        var sharpMult = on ? Settings.GroundSharpness.Value : 1f;

        // The state can share its speed array with the data asset, so the baseline must come from our own snapshot.
        var speeds = state.movementSpeeds;
        if (speeds != null)
        {
            if (!baselineSpeeds.TryGetValue(data.Pointer, out var baseline) || baseline.Length != speeds.Length)
                baselineSpeeds[data.Pointer] = baseline = speeds.ToArray();
            if (appliedSpeeds == null || appliedSpeeds.Length != speeds.Length) appliedSpeeds = (float[])baseline.Clone();
            for (var i = 0; i < speeds.Length; i++)
            {
                var current = speeds[i];
                if (!Mathf.Approximately(current, baseline[i]) && !Mathf.Approximately(current, appliedSpeeds[i])) continue;
                appliedSpeeds[i] = baseline[i] * speedMult;
                if (!Mathf.Approximately(current, appliedSpeeds[i])) speeds[i] = appliedSpeeds[i];
            }
        }
        var sharp = baselineSharpness.TryGetValue(data.Pointer, out var s) ? s
            : baselineSharpness[data.Pointer] = (data.groundAccelerateSharpness, data.groundDecelerateSharpness);
        state.groundAccelerateSharpness = Retarget(state.groundAccelerateSharpness, sharp.accel, sharpMult, ref appliedAccel);
        state.groundDecelerateSharpness = Retarget(state.groundDecelerateSharpness, sharp.decel, sharpMult, ref appliedDecel);
    }

    private static readonly Dictionary<IntPtr, float[]> baselineSpeeds = new();
    private static readonly Dictionary<IntPtr, (float accel, float decel)> baselineSharpness = new();

    private static float Retarget(float current, float baseline, float mult, ref float applied)
    {
        var ours = Mathf.Approximately(current, baseline) || (!float.IsNaN(applied) && Mathf.Approximately(current, applied));
        if (!ours) return current;
        applied = baseline * mult;
        return applied;
    }
}
