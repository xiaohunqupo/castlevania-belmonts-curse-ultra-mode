using HarmonyLib;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;

namespace UltraMode.Features;

/// <summary>Hits on the player resolve to Ignore during the push of a slide or backdash. Traps, death zones and fall damage still apply.</summary>
[Feature("Evasion")]
[HarmonyPatch]
internal static class Evasion
{
    private static readonly Feature Fix = Feature.Get("Evasion");
    private const DamageFlag Environmental = DamageFlag.TrapDamage | DamageFlag.DeathZone | DamageFlag.FallDamage;
    private static readonly Dictionary<IntPtr, bool> cache = new();
    private static string keywordsSource = "";
    private static string[] keywords = Array.Empty<string>();

    internal static bool IsEvasion(ActionData data)
    {
        if (keywordsSource != Settings.EvasionActionKeywords.Value)
        {
            keywordsSource = Settings.EvasionActionKeywords.Value;
            keywords = Settings.Keywords(Settings.EvasionActionKeywords);
            cache.Clear();
        }
        if (cache.TryGetValue(data.Pointer, out var result)) return result;
        var name = data.name.ToLowerInvariant();
        result = keywords.Any(name.Contains)
                 || (Settings.EvasionUsesDodgeFeature.Value && ActionInfo.Feature<DodgeMovementFeature>(data) != null);
        cache[data.Pointer] = result;
        return result;
    }

    /// <summary>
    /// Only the dodge's push (its DodgeMovementFeature impulsion) counts; the recovery into the idle stance after it
    /// is vulnerable as in vanilla.
    /// </summary>
    internal static bool InWindow(Entity e)
    {
        var actor = Player.Comp<Actor>(e);
        if (actor == null) return false;
        var list = actor.activeActions;
        for (var i = 0; i < list.Count; i++)
        {
            var data = list[i].data;
            if (data == null || !IsEvasion(data)) continue;
            var end = ActionInfo.Feature<DodgeMovementFeature>(data)?.impulsionEndFrame ?? -1;
            if (end <= 0 || list[i].frameInfo.integerFrame <= end) return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(HeroHitStrategy), nameof(HeroHitStrategy.GetHitResult)), HarmonyPostfix]
    private static void HeroResult(DamageSystem.Connection connection, ref HitResult __result) => Filter(connection, ref __result);

    [HarmonyPatch(typeof(DefaultHitStrategy), nameof(DefaultHitStrategy.GetHitResult)), HarmonyPostfix]
    private static void DefaultResult(DamageSystem.Connection connection, ref HitResult __result) => Filter(connection, ref __result);

    private static void Filter(DamageSystem.Connection connection, ref HitResult result)
    {
        if (!Fix.Enabled || !Settings.On || !Settings.EvasionInvulnerable.Value || result != HitResult.Success) return;
        try
        {
            if (!Player.Is(connection.target) || !InWindow(connection.target)) return;
            if ((connection.damageData.damageFlags & Environmental) != 0) return;
            result = HitResult.Ignore;
            Log.Debug("Evasion: hit ignored");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
