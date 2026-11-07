using HarmonyLib;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode;

/// <summary>
/// Diffs the player's active action list after each state update. Only the Entity argument is marshalled,
/// which keeps this hook clear of by-value ActionInstance parameters.
/// </summary>
[Feature("ActionTracker")]
[HarmonyPatch]
internal static class ActionTracker
{
    private static readonly Feature Fix = Feature.Get("ActionTracker");
    private static Dictionary<int, ActionData> active = new();
    private static Dictionary<int, ActionData> scratch = new();

    /// <summary>Every action the player started this session, by name, for the F11 dump.</summary>
    internal static readonly Dictionary<string, int> Seen = new();
    internal static readonly Dictionary<string, ActionData> SeenData = new();
    /// <summary>The player's running actions as of the last state update, keyed by ActionInstance.id.</summary>
    internal static IReadOnlyDictionary<int, ActionData> Active => active;

    internal static event Action<Entity, ActionData>? Started;
    internal static event Action<Entity, ActionData>? Stopped;
    /// <summary>Runs once per player state update, after Started/Stopped.</summary>
    internal static event Action<Entity, Actor>? Updated;

    [HarmonyPatch(typeof(Actor), nameof(Actor.UpdateState)), HarmonyPostfix]
    private static void UpdateState(Actor __instance, Entity e)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (!Player.Is(e)) return;
            scratch.Clear();
            var list = __instance.activeActions;
            for (var i = 0; i < list.Count; i++)
            {
                var instance = list[i];
                var data = instance.data;
                if (data == null) continue;
                scratch[instance.id] = data;
                if (active.ContainsKey(instance.id)) continue;

                var name = data.name;
                Seen[name] = Seen.TryGetValue(name, out var n) ? n + 1 : 1;
                SeenData[name] = data;
                Log.Debug($"@{Time.frameCount} + {name} [{data.GetIl2CppType().Name}] end={data.endFrame} chain={data.chainableFrame}");
                Started?.Invoke(e, data);
            }
            foreach (var (id, data) in active)
            {
                if (scratch.ContainsKey(id)) continue;
                Log.Debug($"@{Time.frameCount} - {data.name}");
                Stopped?.Invoke(e, data);
            }
            (active, scratch) = (scratch, active);
            Updated?.Invoke(e, __instance);
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    internal static bool IsActive(Func<ActionData, bool> predicate)
    {
        foreach (var data in active.Values)
            if (predicate(data)) return true;
        return false;
    }
}
