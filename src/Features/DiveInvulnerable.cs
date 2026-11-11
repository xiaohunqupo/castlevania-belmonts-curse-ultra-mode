using HarmonyLib;
using MotherBase.ActionFramework;

namespace UltraMode.Features;

/// <summary>
/// Hits on the player resolve to Ignore while any RiseAndFall action (the Zealot's Chronomantic Dive: takeoff, hang,
/// fall, landing, and the fall carried across a room transition) is active. Traps, death zones and fall damage still apply.
/// </summary>
[Feature("DiveInvulnerable")]
[HarmonyPatch]
internal static class DiveInvulnerable
{
    private static readonly Feature Fix = Feature.Get("DiveInvulnerable");
    private const DamageFlag Environmental = DamageFlag.TrapDamage | DamageFlag.DeathZone | DamageFlag.FallDamage;
    private const string Stem = "RiseAndFall";

    private static bool Diving(MotherBase.EntityFramework.Entity e)
    {
        var actor = Player.Comp<Actor>(e);
        if (actor == null) return false;
        var list = actor.activeActions;
        for (var i = 0; i < list.Count; i++)
            if (list[i].data?.name.Contains(Stem) == true) return true;
        return false;
    }

    [HarmonyPatch(typeof(HeroHitStrategy), nameof(HeroHitStrategy.GetHitResult)), HarmonyPostfix]
    private static void HeroResult(DamageSystem.Connection connection, ref HitResult __result) => Filter(connection, ref __result);

    [HarmonyPatch(typeof(DefaultHitStrategy), nameof(DefaultHitStrategy.GetHitResult)), HarmonyPostfix]
    private static void DefaultResult(DamageSystem.Connection connection, ref HitResult __result) => Filter(connection, ref __result);

    private static void Filter(DamageSystem.Connection connection, ref HitResult result)
    {
        if (!Fix.Enabled || !Settings.On || !Settings.DiveInvulnerable.Value || result != HitResult.Success) return;
        try
        {
            if (!Player.Is(connection.target) || (connection.damageData.damageFlags & Environmental) != 0) return;
            if (!Diving(connection.target)) return;
            result = HitResult.Ignore;
            Log.Debug($"DiveInvulnerable: hit from {connection.source?.name} ignored");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
