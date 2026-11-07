using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode;

/// <summary>Main-character lookups. Feature assets are shared with enemies, so every patch filters through here.</summary>
internal static class Player
{
    private static int cachedFrame = -1;
    private static Entity? cached;

    /// <summary>Looked up once per frame; null while there is no main character (title screen, loading).</summary>
    internal static Entity? Get()
    {
        var frame = Time.frameCount;
        if (frame == cachedFrame) return cached;
        cachedFrame = frame;
        cached = MainCharacterSingleton.TryGetEntity(out var entity) && entity != null ? entity : null;
        return cached;
    }

    /// <summary>Compares native pointers: two interop wrappers of the same object are different managed instances.</summary>
    internal static bool Is(Entity? e)
    {
        if (e == null) return false;
        var mc = Get();
        return mc != null && mc.Pointer == e.Pointer;
    }

    /// <summary>The component is on the player's game object or on one of its children.</summary>
    internal static bool Owns(Component? c)
    {
        if (c == null) return false;
        var mc = Get();
        if (mc == null) return false;
        if (c.gameObject.Pointer == mc.gameObject.Pointer) return true;
        var owner = c.GetComponentInParent<Entity>();
        return owner != null && owner.Pointer == mc.Pointer;
    }

    /// <summary>Some of the player's components sit on child objects, so children are searched too.</summary>
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

/// <summary>Logging that is only written with DebugLogging on; use it for anything that can fire every frame.</summary>
internal static class Log
{
    internal static void Debug(string message)
    {
        if (Settings.DebugLogging.Value) Plugin.Logger.LogInfo(message);
    }
}
