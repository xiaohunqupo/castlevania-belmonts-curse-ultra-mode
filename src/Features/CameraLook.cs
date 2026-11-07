using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;
using Object = UnityEngine.Object;
using PlayerInput = MotherBase.ActionFramework.PlayerInput;

namespace UltraMode.Features;

/// <summary>
/// Right-stick camera look lives in CameraOffset: maxDistance bounds it and currentValue is its only state, eased by
/// native code. The range is scaled from a per-component snapshot, and each frame's native step is amplified, so
/// both looking out and recentering get faster without replacing the game's easing.
/// </summary>
[Feature("CameraLook")]
internal static class CameraLook
{
    private static readonly Feature Fix = Feature.Get("CameraLook");
    private static readonly Dictionary<IntPtr, Vector2> baseline = new();

    private static IntPtr searchedFor;
    private static CameraOffset? offset;
    private static Vector3 written;
    private static bool hasWritten;
    private static float nextLog;

    internal static void Init() => ActionTracker.Updated += OnUpdated;

    private static CameraOffset? Find(Entity e)
    {
        if (searchedFor == e.Pointer && (offset == null || !offset.WasCollected)) return offset;
        searchedFor = e.Pointer;
        hasWritten = false;
        offset = Player.Comp<CameraOffset>(e) ?? Object.FindObjectOfType<CameraOffset>();
        Log.Debug($"CameraLook: CameraOffset = {(offset == null ? "not found" : $"{offset.name} max={offset.maxDistance}")}");
        return offset;
    }

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!Fix.Enabled) return;
        try
        {
            var cam = Find(e);
            if (cam == null) return;

            var on = Settings.On;
            var range = on ? Settings.CameraLookRange.Value : 1f;
            var speed = on ? Settings.CameraLookSpeed.Value : 1f;

            if (!baseline.TryGetValue(cam.Pointer, out var max)) baseline[cam.Pointer] = max = cam.maxDistance;
            var limit = max * range;
            if (cam.maxDistance != limit) cam.maxDistance = limit;

            var current = cam.currentValue;
            if (hasWritten && speed > 1f && current != written)
            {
                var amplified = written + (current - written) * speed;
                current = new Vector3(Step(written.x, current.x, amplified.x, limit.x), Step(written.y, current.y, amplified.y, limit.y), current.z);
                cam.currentValue = current;
            }
            written = current;
            hasWritten = true;

            if (Settings.DebugLogging.Value && Time.unscaledTime >= nextLog)
            {
                var aim = Player.Comp<PlayerInput>(e)?.aimInputValue ?? Vector2.zero;
                if (aim.sqrMagnitude > 0.09f)
                {
                    nextLog = Time.unscaledTime + 1f;
                    Log.Debug($"CameraLook: stick={aim} offset={current} max={limit}");
                }
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    /// <summary>Keeps the amplified step within the range and never past the value native code was heading to when recentering.</summary>
    private static float Step(float from, float native, float amplified, float limit)
    {
        var bound = Mathf.Abs(limit);
        if (bound > 0f) amplified = Mathf.Clamp(amplified, -bound, bound);
        var towardZero = Mathf.Abs(native) < Mathf.Abs(from);
        if (towardZero && Mathf.Sign(amplified) != Mathf.Sign(native) && native != 0f) return 0f;
        if (towardZero && native == 0f) return 0f;
        return amplified;
    }
}
