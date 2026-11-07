using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode;

/// <summary>Main-character lookups. Feature assets are shared with enemies, so every patch filters through here.</summary>
internal static class Player
{
    private static int cachedFrame = -1;
    private static Entity? cached;

    internal static Entity? Get()
    {
        var frame = Time.frameCount;
        if (frame == cachedFrame) return cached;
        cachedFrame = frame;
        cached = MainCharacterSingleton.TryGetEntity(out var entity) && entity != null ? entity : null;
        return cached;
    }

    internal static bool Is(Entity? e)
    {
        if (e == null) return false;
        var mc = Get();
        return mc != null && mc.Pointer == e.Pointer;
    }

    internal static bool Owns(Component? c)
    {
        if (c == null) return false;
        var mc = Get();
        if (mc == null) return false;
        if (c.gameObject.Pointer == mc.gameObject.Pointer) return true;
        var owner = c.GetComponentInParent<Entity>();
        return owner != null && owner.Pointer == mc.Pointer;
    }

    internal static T? Comp<T>(Entity e) where T : Component
    {
        var c = e.GetComponent<T>();
        return c != null ? c : e.GetComponentInChildren<T>();
    }

    internal static bool IsGrounded(Entity e)
    {
        var motor = Comp<CharacterMotor>(e);
        return motor != null && motor.groundingStatus.isStableOnGround;
    }

    internal static Inventory? Inventory(Entity e) => Comp<InventoryComponent>(e)?.inventory;
}

internal static class Log
{
    internal static void Debug(string message)
    {
        if (Settings.DebugLogging.Value) Plugin.Logger.LogInfo(message);
    }
}
