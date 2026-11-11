using HarmonyLib;
using MotherBase.EntityFramework;

namespace UltraMode.Features;

/// <summary>
/// Gaining a level from experience refills HP and spell mana. The level is compared around
/// IncrementCurrentExperience, so loading a save (which sets the level directly) refills nothing.
/// </summary>
[Feature("LevelUpRefill")]
[HarmonyPatch]
internal static class LevelUpRefill
{
    private static readonly Feature Fix = Feature.Get("LevelUpRefill");

    [HarmonyPatch(typeof(ExperienceComponent), nameof(ExperienceComponent.IncrementCurrentExperience)), HarmonyPrefix]
    private static void Before(ExperienceComponent __instance, out int __state)
    {
        __state = -1;
        if (!Fix.Enabled) return;
        try { if (__instance.isMcEntity) __state = __instance.GetLevel(); }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(ExperienceComponent), nameof(ExperienceComponent.IncrementCurrentExperience)), HarmonyPostfix]
    private static void After(ExperienceComponent __instance, Entity e, int __state)
    {
        if (!Fix.Enabled || __state < 0 || !Settings.On || !Settings.LevelUpRefill.Value) return;
        try
        {
            var level = __instance.GetLevel();
            if (level <= __state || !Player.Is(e)) return;
            var health = Player.Comp<Damageable>(e);
            if (health != null) health.SetCurrentHealth(e, health.maxHealth, true);
            var mana = Player.Comp<ManaCaster>(e);
            if (mana != null && mana.spellManaType != null)
                mana.SetCurrentMana(e, mana.spellManaType, mana.GetMaxMana(e, mana.spellManaType), false);
            Log.Debug($"LevelUpRefill: level {__state} -> {level}, HP and MP refilled");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
