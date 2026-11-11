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
    /// <summary>
    /// Names that look like frames but are durations, lengths or ids. Also skips <c>*Length</c> / <c>*Duration</c>
    /// (except fade in/out blends). Charge thresholds and hurtbox windows live in arrays/value types and must still
    /// be collected — see <see cref="CollectCollection"/>.
    /// </summary>
    private static readonly string[] excluded = { "hitstop", "invulnerab", "slowmo", "version", "recoverinvul", "length", "duration", "framedelay", "framebuffer" };

    internal sealed class FrameField
    {
        internal readonly Il2CppObjectBase? Target;
        internal readonly PropertyInfo Prop;
        internal readonly int Original;
        /// <summary>Il2Cpp value types in arrays must be written back through the indexer; <see cref="Prop"/> alone only mutates a boxed copy.</summary>
        internal readonly Action<int>? Write;
        internal FrameField(Il2CppObjectBase? target, PropertyInfo prop, int original, Action<int>? write = null)
        {
            Target = target; Prop = prop; Original = original; Write = write;
        }
        internal void Set(int value)
        {
            if (Write != null) Write(value);
            else if (Target != null) Prop.SetValue(Target, value);
        }
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
    /// <summary>Feature objects can be shared by several actions; only the first plan to reach one rescales it.</summary>
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

    private static bool ShouldScale(string lower) =>
        lower is "fadeinduration" or "fadeoutduration" or "endframe"
        || (lower.Contains("frame") && !excluded.Any(lower.Contains));

    /// <summary>Offsets stored relative to the first hit (not absolute timeline positions).</summary>
    private static bool IsRelativeOffset(string lower) =>
        lower.Contains("afterfirsthit") || lower is "lockafterfirsthitframe" or "lockbeforefirsthitframe";

    private static void Collect(Plan plan, Il2CppObjectBase target, int depth)
    {
        if (!claimed.Add(target.Pointer)) return;
        var obj = (Il2CppObjectBase)Inspector.Resolve(target);
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
            var lower = p.Name.ToLowerInvariant();
            if (p.PropertyType == typeof(int) && p.CanWrite && ShouldScale(lower))
            {
                try { plan.Fields.Add(new FrameField(obj, p, (int)p.GetValue(obj)!)); } catch { }
            }
            else if (depth > 0 && IsCollection(p.PropertyType))
            {
                try
                {
                    if (p.GetValue(obj) is Il2CppObjectBase collection)
                        CollectCollection(plan, collection, depth - 1);
                }
                catch { }
            }
        }
    }

    private static bool IsCollection(Type type)
    {
        var name = type.Name;
        return name.StartsWith("List`1", StringComparison.Ordinal)
            || name.StartsWith("Il2CppReferenceArray`1", StringComparison.Ordinal)
            || name.StartsWith("Il2CppStructArray`1", StringComparison.Ordinal);
    }

    private static void CollectCollection(Plan plan, Il2CppObjectBase collection, int depth)
    {
        if (!TryGetIndexer(collection, out var item, out var count)) return;
        for (var i = 0; i < count; i++)
        {
            object? raw;
            try { raw = item.GetValue(collection, new object[] { i }); } catch { continue; }
            if (raw == null) continue;

            if (raw is Il2CppObjectBase element and not UnityEngine.Object and not Il2CppSystem.ValueType)
            {
                Collect(plan, element, depth);
                continue;
            }

            // ChargeStep and similar: int frame fields on a value type stored in an array.
            if (raw is not Il2CppSystem.ValueType && !raw.GetType().IsValueType) continue;
            CollectValueTypeElement(plan, collection, item, i, raw);
        }
    }

    private static void CollectValueTypeElement(Plan plan, Il2CppObjectBase collection, PropertyInfo item, int index, object boxed)
    {
        foreach (var p in boxed.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite || p.PropertyType != typeof(int) || p.GetIndexParameters().Length != 0) continue;
            var lower = p.Name.ToLowerInvariant();
            if (!ShouldScale(lower)) continue;
            int original;
            try { original = (int)p.GetValue(boxed)!; } catch { continue; }
            var i = index;
            var prop = p;
            plan.Fields.Add(new FrameField(null, prop, original, value =>
            {
                var current = item.GetValue(collection, new object[] { i });
                if (current == null) return;
                prop.SetValue(current, value);
                item.SetValue(collection, current, new object[] { i });
            }));
        }
    }

    private static bool TryGetIndexer(Il2CppObjectBase collection, out PropertyInfo item, out int count)
    {
        var type = collection.GetType();
        item = type.GetProperty("Item")!;
        if (item == null) { count = 0; return false; }
        if (type.GetProperty("Count")?.GetValue(collection) is int c) { count = c; return true; }
        if (type.GetProperty("Length")?.GetValue(collection) is int l) { count = l; return true; }
        count = 0;
        return false;
    }

    private static int Map(Plan plan, int frame, float windup, float recovery)
    {
        if (frame <= 0) return frame;
        var h = plan.FirstHit;
        var mapped = h > 0 && frame > h ? h / windup + (frame - h) / recovery : frame / windup;
        return Math.Max(1, Mathf.RoundToInt(mapped));
    }

    private static int MapField(Plan plan, FrameField field, float windup, float recovery)
    {
        if (field.Original <= 0) return field.Original;
        var lower = field.Prop.Name.ToLowerInvariant();
        if (IsRelativeOffset(lower))
        {
            var speed = lower.Contains("before") ? windup : recovery;
            return Math.Max(0, Mathf.RoundToInt(field.Original / speed));
        }
        return Map(plan, field.Original, windup, recovery);
    }

    private static void Apply(Plan plan)
    {
        var (w, r) = Settings.On ? plan.Speeds() : (1f, 1f);
        var mapped = new int[plan.Fields.Count];
        for (var i = 0; i < plan.Fields.Count; i++)
        {
            var f = plan.Fields[i];
            mapped[i] = Settings.On && f.Original > 0 && plan.Pinned?.Invoke(f) is { } pin
                ? pin
                : MapField(plan, f, w, r);
        }

        // Rounding can collapse a 1–2 frame hurtbox window to start==end; keep at least one active frame.
        if (Settings.On)
            PreserveWindows(plan, mapped, Math.Max(w, r));

        for (var i = 0; i < plan.Fields.Count; i++)
        {
            try { plan.Fields[i].Set(mapped[i]); } catch { }
        }

        if (plan.Data.scaleMode != ActionData.ScaleMode.EndFrame && plan.OriginalEnd > 0)
        {
            var newEnd = Map(plan, plan.OriginalEnd, w, r);
            plan.Data.animTimeScale = plan.OriginalTimeScale * plan.OriginalEnd / newEnd;
        }
        else if (plan.ScaleOpenEnded && plan.OriginalEnd <= 0)
            plan.Data.animTimeScale = plan.OriginalTimeScale * w;
    }

    private static void PreserveWindows(Plan plan, int[] mapped, float speed)
    {
        for (var i = 0; i < plan.Fields.Count; i++)
        {
            var end = plan.Fields[i];
            var endName = end.Prop.Name;
            if (endName is not ("endFrame" or "stopFrame" or "activationStopFrame" or "projectileEndFrame")) continue;
            if (end.Target == null) continue;
            for (var j = 0; j < plan.Fields.Count; j++)
            {
                var start = plan.Fields[j];
                if (start.Target?.Pointer != end.Target.Pointer) continue;
                var startName = start.Prop.Name;
                if (startName is not ("startFrame" or "activationStartFrame" or "projectileStartFrame")) continue;
                if (!NamesPair(startName, endName)) continue;
                if (start.Original >= end.Original) continue;
                if (mapped[i] > mapped[j]) continue;
                var minSpan = Math.Max(1, Mathf.RoundToInt((end.Original - start.Original) / speed));
                mapped[i] = mapped[j] + minSpan;
            }
        }
    }

    private static bool NamesPair(string start, string end) => (start, end) switch
    {
        ("startFrame", "endFrame") => true,
        ("startFrame", "stopFrame") => true,
        ("activationStartFrame", "activationStopFrame") => true,
        ("projectileStartFrame", "projectileEndFrame") => true,
        _ => false,
    };

    internal static void ReapplyAll()
    {
        foreach (var plan in plans.Values)
        {
            if (plan.Data == null || plan.Data.WasCollected) continue;
            Apply(plan);
        }
    }
}

/// <summary>
/// Decides which actions <see cref="FrameScaler"/> rescales (attacks, whip, turn/stop reactions, crouch and mirror)
/// and retargets ground speed and acceleration on the player's movement state every update.
/// </summary>
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
