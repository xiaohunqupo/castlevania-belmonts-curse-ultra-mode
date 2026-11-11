using HarmonyLib;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// A broken experience statue (GPE_DestructibleSourceExperience) drops experience balls that reach the player over
/// time. Once they stop arriving, if the player has not gained a level since the drop, the rest of the current
/// level's experience is added, so the game's own level-up runs (and LevelUpRefill with it).
/// </summary>
[Feature("StatueLevelUp")]
[HarmonyPatch]
internal static class StatueLevelUp
{
    private static readonly Feature Fix = Feature.Get("StatueLevelUp");
    private const string StatueName = "DestructibleSourceExperience";
    /// <summary>Frames without new experience after which the statue's balls count as collected.</summary>
    private const int Settle = 90;
    private const int Timeout = 900;

    private static int levelAtDrop = -1;
    private static int dropFrame, lastGainFrame;

    internal static void Init() => ActionTracker.Updated += (e, _) =>
    {
        if (!Fix.Enabled || levelAtDrop < 0) return;
        try { Check(e); }
        catch (Exception ex) { Fix.Fail(ex); }
    };

    [HarmonyPatch(typeof(ExperienceLootData), nameof(ExperienceLootData.Drop)), HarmonyPostfix]
    private static void Drop(Entity droppingEntity, Entity lootingEntity)
    {
        if (!Fix.Enabled || !Settings.On || !Settings.StatueLevelUp.Value) return;
        try
        {
            var name = droppingEntity != null ? droppingEntity.name : "";
            Log.Debug($"StatueLevelUp: experience dropped by {name}");
            if (!name.Contains(StatueName, StringComparison.OrdinalIgnoreCase)) return;
            var mc = Player.Get();
            var xp = mc != null ? Player.Comp<ExperienceComponent>(mc) : null;
            if (xp == null) return;
            if (levelAtDrop < 0) levelAtDrop = xp.GetLevel();
            dropFrame = lastGainFrame = Time.frameCount;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(ExperienceComponent), nameof(ExperienceComponent.IncrementCurrentExperience)), HarmonyPostfix]
    private static void Gained(ExperienceComponent __instance)
    {
        if (levelAtDrop >= 0 && __instance.isMcEntity) lastGainFrame = Time.frameCount;
    }

    private static void Check(Entity e)
    {
        var frame = Time.frameCount;
        if (frame - lastGainFrame < Settle && frame - dropFrame < Timeout) return;
        var start = levelAtDrop;
        levelAtDrop = -1;
        if (!Settings.On || !Settings.StatueLevelUp.Value) return;
        var xp = Player.Comp<ExperienceComponent>(e);
        if (xp == null || xp.GetLevel() > start) return;
        var missing = xp.GetExperienceMax() - xp.GetExperienceCurrent();
        if (missing <= 0) return;
        Log.Debug($"StatueLevelUp: statue left level {start} short by {missing}, adding it");
        xp.IncrementCurrentExperience(e, missing);
    }
}
