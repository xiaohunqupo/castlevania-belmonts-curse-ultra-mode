using HarmonyLib;
using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// The player's parry action (MC_VaniaLight_Parry) works through three features over frames 2-7: ParryFeature,
/// ChangeHitReceptionStrategy (swaps in VaniaLightHitStrategy) and ToggleColliderListFeature (the parry area).
/// Once the parry is unlocked, every attack opens that same window by driving those three pieces directly.
/// </summary>
[Feature("AttackParry")]
[HarmonyPatch]
internal static class AttackParry
{
    private static readonly Feature Fix = Feature.Get("AttackParry");
    /// <summary>Key our hit strategy is added and removed under; must not collide with the ids of the game's own features.</summary>
    private const int StrategyFeatureId = 0x55_17_4A;

    private static ActionData? parryAction;
    private static ParryFeature? parry;
    private static ChangeHitReceptionStrategy? strategy;
    private static IntPtr resolvedFor;
    private static bool searched, unlocked;
    private static int nextUnlockCheck;

    private static int trackedId = -1;
    private static ActionInstance? trackedInstance;
    private static bool opened, closed;

    internal static void Init()
    {
        ActionTracker.Started += OnStarted;
        ActionTracker.Updated += OnUpdated;
    }

    /// <summary>Every loaded action with a ParryFeature, enemies' included; also written to the F11 dump.</summary>
    internal static IEnumerable<ActionData> Candidates()
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<ActionData>()))
        {
            var data = o.TryCast<ActionData>();
            if (data != null && ActionInfo.Feature<ParryFeature>(data) != null) yield return data;
        }
    }

    private static bool IsParryName(string name) => string.Equals(name, Settings.ParryActionName.Value.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Use(ActionData data)
    {
        parryAction = data;
        parry = ActionInfo.Feature<ParryFeature>(data);
        strategy = ActionInfo.Feature<ChangeHitReceptionStrategy>(data);
        Log.Debug($"AttackParry: using {data.name} parry={parry != null} backParry={parry?.backParry} strategy={strategy?.newHitStrategy?.name ?? "none"}");
    }

    private static void OnStarted(Entity e, ActionData data)
    {
        if (!Fix.Enabled || !IsParryName(data.name)) return;
        try
        {
            if (parryAction == null || parryAction.Pointer != data.Pointer) Use(data);
            unlocked = true;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    /// <summary>One asset scan per player entity, only needed when the parry has not been used yet this session.</summary>
    private static void Resolve(Entity e)
    {
        if (resolvedFor != e.Pointer)
        {
            resolvedFor = e.Pointer;
            searched = false;
            unlocked = false;
        }
        if (parryAction != null && !parryAction.WasCollected) return;
        if (searched) return;
        searched = true;
        var found = Candidates().FirstOrDefault(c => IsParryName(c.name));
        if (found != null) Use(found);
        else Log.Debug($"AttackParry: '{Settings.ParryActionName.Value}' not loaded yet");
    }

    /// <summary>The parry action can't start until the parry is unlocked; polled every 30 frames until it can.</summary>
    private static bool CheckUnlocked(Entity e)
    {
        if (unlocked || parryAction == null || Time.frameCount < nextUnlockCheck) return unlocked;
        nextUnlockCheck = Time.frameCount + 30;
        if (parryAction.CanStart(e))
        {
            unlocked = true;
            Log.Debug("AttackParry: parry unlocked");
        }
        return unlocked;
    }

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (!Settings.On || !Settings.AttackParry.Value) { Close(e); return; }
            Resolve(e);
            if (parry == null || !CheckUnlocked(e)) return;

            ActionInstance? attack = null;
            var list = actor.activeActions;
            for (var i = 0; i < list.Count; i++)
            {
                var instance = list[i];
                var data = instance.data;
                if (data == null) continue;
                if (data.Pointer == parryAction!.Pointer) { Close(e); return; }
                if (ActionInfo.IsAttack(data)) attack = instance;
            }

            if (attack == null || attack.id != trackedId)
            {
                Close(e);
                if (attack == null) return;
                trackedId = attack.id;
                opened = closed = false;
            }
            trackedInstance = attack;

            var frame = attack.frameInfo.integerFrame;
            var start = parry.shouldStartParry ? parry.startParryFrame : 0;
            var stop = parry.shouldStopParry ? parry.stopParryFrame : parryAction!.endFrame;
            if (!opened && frame >= start) Open(e, attack);
            if (opened && !closed && frame >= stop) Close(e);
        }
        catch (Exception ex) { Fix.Fail(ex); Close(e); }
    }

    private static void Open(Entity e, ActionInstance attack)
    {
        opened = true;
        parry!.StartParry(e, attack);
        if (strategy?.newHitStrategy != null) ChangeHitReceptionStrategy.AddStrategy(e, StrategyFeatureId, strategy.newHitStrategy);
        Player.Comp<ColliderListComponent>(e)?.EnableColliders();
        Log.Debug($"AttackParry: open on {attack.data.name} frame={attack.frameInfo.integerFrame}");
    }

    private struct ApplyState
    {
        internal ActionTrigger? success;
        internal bool interrupt;
        internal ActionData? successData;
    }

    /// <summary>
    /// A parry opened by an attack must not replace the attack: the success action (its own animation) is withheld
    /// and the attack is not interrupted, only the success action's feedbacks (VFX, sound, shake) are played.
    /// The enemy-side parry reaction is untouched.
    /// </summary>
    [HarmonyPatch(typeof(ParryState), nameof(ParryState.OnApplyParry)), HarmonyPrefix]
    private static bool OnApplyParryPrefix(ParryState __instance, Entity parryEntity, out ApplyState __state)
    {
        __state = default;
        if (!Fix.Enabled || !Settings.On) return true;
        try
        {
            var from = __instance.fromAction;
            if (from?.data == null || !ActionInfo.IsAttack(from.data) || !Player.Is(parryEntity)) return true;
            if (Protected) return false;
            __state.success = __instance.successAction;
            __state.interrupt = __instance.shouldInterruptParryAction;
            __state.successData = __state.success?.GetActionData(parryEntity);
            __instance.successAction = null;
            __instance.shouldInterruptParryAction = false;
        }
        catch (Exception ex) { Fix.Fail(ex); }
        return true;
    }

    [HarmonyPatch(typeof(ParryState), nameof(ParryState.OnApplyParry)), HarmonyPostfix]
    private static void OnApplyParryPostfix(ParryState __instance, Entity parryEntity, ApplyState __state)
    {
        if (__state.success == null && __state.successData == null) return;
        try
        {
            __instance.successAction = __state.success;
            __instance.shouldInterruptParryAction = __state.interrupt;
            var attack = __instance.fromAction;
            var played = PlayFeedbacks(parryEntity, __state.successData, attack);
            var frames = __state.successData != null && __state.successData.endFrame > 0 ? __state.successData.endFrame : FallbackProtectFrames;
            protectedUntil = Time.time + frames / GameFps;
            Log.Debug($"AttackParry: parried during {attack?.data?.name}, kept attack, played {played} feedback(s) of {__state.successData?.name ?? "none"}, follow-up hits ignored for {frames} frames");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private const float GameFps = 60f;
    private const int FallbackProtectFrames = 30;
    private const DamageFlag Environmental = DamageFlag.TrapDamage | DamageFlag.DeathZone | DamageFlag.FallDamage;
    private static float protectedUntil = -1f;
    private static bool Protected => Time.time < protectedUntil;

    /// <summary>
    /// A regular parry hands over to its success action, which shrugs off the rest of the enemy's attack. The attack
    /// parry keeps the attack running instead, so for as long as that success action would last, further hits are
    /// ignored rather than parried again and again.
    /// </summary>
    [HarmonyPatch(typeof(HeroHitStrategy), nameof(HeroHitStrategy.GetHitResult)), HarmonyPostfix]
    private static void HeroResult(DamageSystem.Connection connection, ref HitResult __result) => IgnoreFollowUp(connection, ref __result);

    [HarmonyPatch(typeof(DefaultHitStrategy), nameof(DefaultHitStrategy.GetHitResult)), HarmonyPostfix]
    private static void DefaultResult(DamageSystem.Connection connection, ref HitResult __result) => IgnoreFollowUp(connection, ref __result);

    private static void IgnoreFollowUp(DamageSystem.Connection connection, ref HitResult result)
    {
        if (!Protected || result is not (HitResult.Success or HitResult.Parried) || !Fix.Enabled || !Settings.On) return;
        try
        {
            if (!Player.Is(connection.target) || (connection.damageData.damageFlags & Environmental) != 0) return;
            result = HitResult.Ignore;
            Log.Debug("AttackParry: follow-up hit ignored");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static int PlayFeedbacks(Entity e, ActionData? data, ActionInstance? on)
    {
        var features = data?.actionFeatures;
        if (features == null || on == null) return 0;
        var played = 0;
        for (var i = 0; i < features.Count; i++)
        {
            var launcher = features[i]?.TryCast<FeedbackLauncherFeature>();
            if (launcher?.feedbackAsset == null || !launcher.CanStartFeedback(e)) continue;
            launcher.StartFeedbackDirectly(e, launcher.feedbackAsset, on);
            played++;
        }
        return played;
    }

    private static void Close(Entity e)
    {
        if (opened && !closed)
        {
            closed = true;
            try { if (trackedInstance != null) parry?.StopParry(e, trackedInstance); } catch { }
            try { ChangeHitReceptionStrategy.RemoveStrategy(e, StrategyFeatureId); } catch { }
            try { Player.Comp<ColliderListComponent>(e)?.DisableColliders(); } catch { }
        }
    }
}
