using Cinemachine;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// The follow lag comes from Cinemachine damping on each room's virtual camera. The active camera's body and
/// confiner damping are divided by the follow speed. Values the game changed on its own (e.g. zeroed for a
/// transition) are left alone: only the snapshot baseline or our own last write is retargeted.
/// </summary>
[Feature("CameraFollow")]
internal static class CameraFollow
{
    private static readonly Feature Fix = Feature.Get("CameraFollow");

    private sealed class Slot
    {
        internal readonly float Baseline;
        internal float Applied = float.NaN;
        internal Slot(float baseline) => Baseline = baseline;

        internal float Retarget(float current, float divisor)
        {
            var ours = Mathf.Approximately(current, Baseline) || (!float.IsNaN(Applied) && Mathf.Approximately(current, Applied));
            if (!ours) return current;
            Applied = Baseline / divisor;
            return Applied;
        }
    }

    private static readonly Dictionary<(IntPtr, int), Slot> slots = new();
    private static CinemachineBrain? brain;
    private static IntPtr loggedCamera;

    internal static void Init() => ActionTracker.Updated += OnUpdated;

    private static float Tune(Il2CppSystem.Object owner, int field, float current, float divisor)
    {
        var key = (owner.Pointer, field);
        if (!slots.TryGetValue(key, out var slot)) slots[key] = slot = new Slot(current);
        return slot.Retarget(current, divisor);
    }

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (brain == null || brain.WasCollected)
            {
                var main = Camera.main;
                brain = main != null ? main.GetComponent<CinemachineBrain>() : null;
                if (brain == null) return;
            }
            var vcam = brain.ActiveVirtualCamera?.TryCast<CinemachineVirtualCamera>();
            if (vcam == null) return;

            var divisor = Settings.On ? Settings.CameraFollowSpeed.Value : 1f;
            var body = vcam.GetCinemachineComponent(CinemachineCore.Stage.Body);
            var framing = body?.TryCast<CinemachineFramingTransposer>();
            if (framing != null)
            {
                framing.m_XDamping = Tune(framing, 0, framing.m_XDamping, divisor);
                framing.m_YDamping = Tune(framing, 1, framing.m_YDamping, divisor);
                framing.m_ZDamping = Tune(framing, 2, framing.m_ZDamping, divisor);
            }
            var dolly = body?.TryCast<CinemachineTrackedDolly>();
            if (dolly != null)
            {
                dolly.m_XDamping = Tune(dolly, 0, dolly.m_XDamping, divisor);
                dolly.m_YDamping = Tune(dolly, 1, dolly.m_YDamping, divisor);
                dolly.m_ZDamping = Tune(dolly, 2, dolly.m_ZDamping, divisor);
            }
            var confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner != null) confiner.m_Damping = Tune(confiner, 0, confiner.m_Damping, divisor);

            if (Settings.DebugLogging.Value && loggedCamera != vcam.Pointer)
            {
                loggedCamera = vcam.Pointer;
                Log.Debug($"CameraFollow: {vcam.name} body={body?.GetIl2CppType().Name ?? "none"} " +
                          (framing != null ? $"damping=({framing.m_XDamping:0.##},{framing.m_YDamping:0.##},{framing.m_ZDamping:0.##}) " : "") +
                          $"confiner={(confiner != null ? confiner.m_Damping.ToString("0.##") : "none")}");
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
