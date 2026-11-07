using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using MotherBase.FeedbackManager;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// Marks the invulnerable part of a slide or backdash with the speed-boost relic's colour flash. It tints the
/// character instead of hiding it, so the movement stays readable. The looping feedback runs for exactly the window.
/// </summary>
[Feature("EvasionBlink")]
internal static class EvasionBlink
{
    private static readonly Feature Fix = Feature.Get("EvasionBlink");
    private const string FeedbackName = "Charm_BoostSpeed_Feedback";

    private static Feedback? flash;
    private static bool resolved;
    private static (Entity entity, Il2CppSystem.Guid guid)? playing;

    private static bool Active => Fix.Enabled && Settings.On && Settings.EvasionInvulnerable.Value;

    internal static void Init()
    {
        ActionTracker.Updated += (e, _) =>
        {
            if (!Fix.Enabled) return;
            try { Update(e); }
            catch (Exception ex) { Stop(); Fix.Fail(ex); }
        };
    }

    private static void Update(Entity e)
    {
        var evading = Active && Evasion.InWindow(e);
        if (!evading)
        {
            Stop();
            return;
        }
        if (playing != null) return;
        if (!resolved)
        {
            resolved = true;
            flash = Resolve();
        }
        if (flash != null) playing = (e, FeedbackUtils.StartFeedback(e, flash));
    }

    private static void Stop()
    {
        if (playing is not { } p) return;
        playing = null;
        try
        {
            if (p.entity != null && !p.entity.WasCollected) FeedbackUtils.StopFeedback(p.entity, p.guid);
        }
        catch { }
    }

    private static Feedback? Resolve()
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Feedback>()))
        {
            var f = o.TryCast<Feedback>();
            if (f == null || f.name != FeedbackName) continue;
            var parts = new List<string>();
            var subs = f.subAssets;
            if (subs != null)
                for (var i = 0; i < subs.Count; i++)
                    if (subs[i] != null) parts.Add(subs[i].GetIl2CppType().Name);
            Log.Debug($"EvasionBlink: using {f.name} [{string.Join(", ", parts)}]");
            return f;
        }
        Log.Debug($"EvasionBlink: {FeedbackName} not found");
        return null;
    }
}
