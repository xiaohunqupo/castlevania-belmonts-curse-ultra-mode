using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using MotherBase.FeedbackManager;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = MotherBase.ActionFramework.PlayerInput;

namespace UltraMode.Features;

/// <summary>
/// Pressing jump while knocked away (MC_Projection and its landing/stun follow-ups) ends the reaction at once and
/// hands control back to base movement, with a flash borrowed from a relic's feedback. Nothing happens while a
/// cutscene or script has disabled player input.
/// </summary>
[Feature("TechRecovery")]
internal static class TechRecovery
{
    private static readonly Feature Fix = Feature.Get("TechRecovery");
    private const string KnockbackPrefix = "MC_Projection";
    private const int MinFrame = 2;
    private static readonly string[] feedbackCharms = { "Charm_TankOneHit", "Charm_InvulnerabilityAfterSave" };

    private const int FlashFrames = 45;
    private const int HaltFrames = 2;
    private static int haltUntil = -1;
    private static IntPtr knockbackData;
    private static int knockbackSince;

    /// <summary>
    /// Stopping the reaction leaves its launch/roll velocity on the character, which the follow-up jump inherits.
    /// The first call clears it all; the following frames only clear what is left horizontally.
    /// </summary>
    private static void Halt(Entity e, bool vertical)
    {
        var velocity = Player.Comp<ActionVelocity>(e);
        if (velocity == null) return;
        var v = velocity.value;
        velocity.value = new Vector3(0f, vertical ? 0f : v.y, v.z);
        var last = velocity.lastVelocity;
        velocity.lastVelocity = new Vector3(0f, vertical ? 0f : last.y, last.z);
    }

    private static IntPtr inputFor;
    private static InputAction? jump;
    private static Feedback? flash;
    private static bool flashResolved;
    private static (Entity entity, Il2CppSystem.Guid guid, int stopAt)? playing;

    internal static void Init()
    {
        ActionTracker.Updated += OnUpdated;
        Settings.OnChanged(() => flashResolved = false, Settings.TechFeedback);
    }

    private static InputAction? Jump(Entity e)
    {
        var input = Player.Comp<PlayerInput>(e);
        if (input == null) return null;
        if (inputFor == input.Pointer) return jump;
        inputFor = input.Pointer;
        jump = null;
        var bindings = input.data?.inputBindings;
        if (bindings != null)
            foreach (var binding in bindings)
            {
                var action = binding?.input?.action;
                if (action == null || !action.name.Contains("jump", StringComparison.OrdinalIgnoreCase)) continue;
                jump = action;
                break;
            }
        Log.Debug($"TechRecovery: jump input = {jump?.actionMap?.name}/{jump?.name ?? "not found"}");
        return jump;
    }

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (playing is { } p && Time.frameCount >= p.stopAt)
            {
                playing = null;
                if (p.entity != null && !p.entity.WasCollected) FeedbackUtils.StopFeedback(p.entity, p.guid);
            }
            if (Time.frameCount <= haltUntil) Halt(e, vertical: false);
            if (!Settings.On || !Settings.TechRecovery.Value) return;

            var list = actor.activeActions;
            ActionInstance? knockback = null;
            for (var i = 0; i < list.Count; i++)
            {
                var d = list[i].data;
                if (d != null && d.name.StartsWith(KnockbackPrefix, StringComparison.Ordinal)) { knockback = list[i]; break; }
            }
            if (knockback == null) { knockbackData = IntPtr.Zero; return; }
            var data = knockback.data;
            if (knockbackData != data.Pointer)
            {
                knockbackData = data.Pointer;
                knockbackSince = Time.frameCount;
            }
            var action = Jump(e);
            if (action == null || !action.WasPressedThisFrame()) return;
            if (LockedFromOutside(e, actor))
            {
                Log.Debug($"TechRecovery: jump during {data.name} ignored, input is locked by a cutscene or script");
                return;
            }
            var elapsed = Time.frameCount - knockbackSince;
            if (elapsed < MinFrame)
            {
                Log.Debug($"TechRecovery: jump during {data.name} ignored, only {elapsed} frame(s) in");
                return;
            }

            var name = data.name;
            var before = Player.Comp<ActionVelocity>(e)?.value;
            actor.StopAction(e, knockback, null);
            var stopped = !IsActive(actor, data);
            Halt(e, vertical: true);
            haltUntil = Time.frameCount + HaltFrames;
            Flash(e);
            knockbackData = IntPtr.Zero;
            Log.Debug($"TechRecovery: recovered from {name} after {elapsed} frames (stopped={stopped}), velocity was {before?.ToString() ?? "unknown"}");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    /// <summary>
    /// Knockback actions lock input themselves, so only locks beyond those of the running actions count: cutscenes
    /// and scripted sequences add theirs through affects.
    /// </summary>
    private static bool LockedFromOutside(Entity e, Actor actor)
    {
        var flag = Player.Comp<RedWineDisableInputFlag>(e);
        if (flag == null || flag.flagCounter <= 0) return false;
        var own = 0;
        var list = actor.activeActions;
        for (var i = 0; i < list.Count; i++)
        {
            var features = list[i].data?.actionFeatures;
            if (features == null) continue;
            for (var j = 0; j < features.Count; j++)
                if (features[j]?.TryCast<LockControlsFeature>() != null) own++;
        }
        return flag.flagCounter > own;
    }

    private static bool IsActive(Actor actor, ActionData data)
    {
        var list = actor.activeActions;
        for (var i = 0; i < list.Count; i++)
            if (list[i].data?.Pointer == data.Pointer) return true;
        return false;
    }

    private static void Flash(Entity e)
    {
        if (!flashResolved)
        {
            flashResolved = true;
            flash = ResolveFlash();
            Log.Debug($"TechRecovery: flash feedback = {flash?.name ?? "none"}");
        }
        if (flash == null) return;
        if (playing is { } p && p.entity != null && !p.entity.WasCollected) FeedbackUtils.StopFeedback(p.entity, p.guid);
        var guid = FeedbackUtils.StartFeedback(e, flash);
        playing = flash.hasLifetime ? null : (e, guid, Time.frameCount + FlashFrames);
    }

    /// <summary>The configured feedback by name, else a relic feedback that ends on its own.</summary>
    private static Feedback? ResolveFlash()
    {
        var wanted = Settings.TechFeedback.Value.Trim();
        if (wanted.Length > 0)
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Feedback>()))
            {
                var f = o.TryCast<Feedback>();
                if (f != null && string.Equals(f.name, wanted, StringComparison.OrdinalIgnoreCase)) return f;
            }
            Log.Debug($"TechRecovery: feedback '{wanted}' not found, falling back to relic feedbacks");
        }

        var charms = new Dictionary<string, CharmItemData>();
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CharmItemData>()))
        {
            var c = o.TryCast<CharmItemData>();
            if (c != null) charms[c.name] = c;
        }
        foreach (var charmName in feedbackCharms)
        {
            if (!charms.TryGetValue(charmName, out var charm)) continue;
            var subs = charm.charmAffect?.subAssets;
            if (subs != null)
                for (var i = 0; i < subs.Count; i++)
                {
                    var launcher = subs[i]?.TryCast<FeedbackLauncherAffectFeature>();
                    if (launcher == null) continue;
                    foreach (var f in new[] { launcher.onRemoveAffectFeedback, launcher.feedbackAsset })
                        if (Finite(f)) return f;
                }
            if (Finite(charm.feedbackOnEquipped)) return charm.feedbackOnEquipped;
        }
        return null;
    }

    private static bool Finite(Feedback? f) => f != null && f.hasLifetime && f.lifetimeInFrame > 0;
}
