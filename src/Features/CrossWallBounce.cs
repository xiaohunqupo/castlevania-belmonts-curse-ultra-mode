using HarmonyLib;
using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// The Holy Cross projectile (BoomerangCross) normally counts wall hits and runs
/// <c>BoomerangCrossProjectile_Projectile_Destroy</c> once the count is high enough. With this on, the count never
/// rises and that destroy action is replaced by the bounce action, so the cross keeps bouncing off walls until it
/// comes back to the player (or times out the same way as vanilla).
/// </summary>
[Feature("CrossWallBounce")]
[HarmonyPatch]
internal static class CrossWallBounce
{
    private static readonly Feature Fix = Feature.Get("CrossWallBounce");
    private const string Destroy = "BoomerangCrossProjectile_Projectile_Destroy";
    private const string Bounce = "BoomerangCrossProjectile_Projectile_BounceWall";

    private static bool Active => Fix.Enabled && Settings.On && Settings.CrossWallBounce.Value;

    private static ActionData? bounce;

    [HarmonyPatch(typeof(BoomerangCrossIncreaseHitCount), nameof(BoomerangCrossIncreaseHitCount.OnStart)), HarmonyPrefix]
    private static bool NoIncrease() => !Active;

    [HarmonyPatch(typeof(BoomerangCrossStateComponent), nameof(BoomerangCrossStateComponent.IncrementCount)), HarmonyPrefix]
    private static bool NoCount() => !Active;

    /// <summary>Wall collision starts Destroy when the hit count is high enough; bounce instead.</summary>
    [HarmonyPatch(typeof(Actor), nameof(Actor.StartAction), typeof(Entity), typeof(ActionTrigger)), HarmonyPrefix]
    private static bool StartDestroy(Actor __instance, Entity e, ref ActionTrigger trigger)
    {
        if (!Active || trigger == null || !trigger.name.Contains(Destroy, StringComparison.Ordinal)) return true;
        try
        {
            if (e.GetComponent<BoomerangCrossStateComponent>() == null) return true;
            var data = BounceAction();
            if (data == null) return true;
            trigger = data;
            Log.Debug($"CrossWallBounce: {Destroy} -> {data.name}");
            return true;
        }
        catch (Exception ex) { Fix.Fail(ex); return true; }
    }

    private static ActionData? BounceAction()
    {
        if (bounce != null && !bounce.WasCollected) return bounce;
        bounce = null;
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<ActionData>()))
        {
            var data = o.TryCast<ActionData>();
            if (data != null && data.name.Contains(Bounce, StringComparison.Ordinal)) { bounce = data; break; }
        }
        return bounce;
    }
}
