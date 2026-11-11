using System.Text.RegularExpressions;
using HarmonyLib;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = MotherBase.ActionFramework.PlayerInput;

namespace UltraMode.Features;

/// <summary>
/// D-pad swaps gear without moving: left/right cycles the highest owned tier of each weapon family (pause-menu order),
/// up/down cycles owned spells that are not assigned to a Quick Cast combo (tarot-page order). Each axis has its own
/// setting. The left stick still moves; when either setting is on, the gamepad dpad contribution to
/// <see cref="PlayerInput.moveInputValue"/> is cleared. Equips wait until the current attack ends or reaches its
/// chainable frame so mid-swing swaps do not interrupt the animation.
/// </summary>
[Feature("QuickEquip")]
[HarmonyPatch]
internal static class QuickEquip
{
    private static readonly Feature Fix = Feature.Get("QuickEquip");
    private const float StickDeadzone = 0.25f;

    /// <summary>Pause-menu weapon order: 短剑、盾牌、矛、全套、大剑、双刀、鞭子.</summary>
    private static readonly string[] WeaponFamilies =
    {
        "WeaponOneHanded", "WeaponAndShield", "WeaponSpear", "WeaponFist", "WeaponTwoHanded", "WeaponDualWield", "WeaponWhip",
    };

    /// <summary>Tarot page order: 火球(净化)、十字架、业火、石化、塔洛斯重拳、艺术咆哮、血刺、摄魂之镰、混沌之轮、冰封之途、时空飞冲、生命汲取、彩绘玻璃盾、正义锁链、镜行回溯.</summary>
    private static readonly string[] SpellOrder =
    {
        "MassPurification", "CursedFire", "BoomerangCross", "FireSeismicWave", "PetrifyingRay", "GiantFist",
        "InvocationStone", "BloodSpikes", "LaserScytheSlash", "TargetingChargingWheels", "FreezingMist", "RiseAndFall",
        "LifeDrain", "GlassArmor", "Detainment", "PocketMirror",
    };

    private static readonly Regex Tier = new(@"Tier(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static WeaponItemData? pendingWeapon;
    private static SpellItemData? pendingSpell;

    private static bool WeaponsOn => Fix.Enabled && Settings.On && Settings.QuickEquipWeapons.Value;
    private static bool SpellsOn => Fix.Enabled && Settings.On && Settings.QuickEquipSpells.Value;
    private static bool SuppressMove => WeaponsOn || SpellsOn;

    internal static void Init() => ActionTracker.Updated += OnUpdated;

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!WeaponsOn && !SpellsOn)
        {
            pendingWeapon = null;
            pendingSpell = null;
            return;
        }
        if (pendingWeapon == null && pendingSpell == null) return;
        try { TryFlush(e, actor); }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    internal static void PollInput()
    {
        if (!WeaponsOn && !SpellsOn) return;
        try
        {
            var pad = Gamepad.current;
            var e = Player.Get();
            if (pad == null || e == null) return;
            var input = Player.Comp<PlayerInput>(e);
            if (input == null || !input.isActiveAndEnabled) return;

            WeaponItemData? weapon = null;
            SpellItemData? spell = null;
            if (WeaponsOn && pad.dpad.right.wasPressedThisFrame) weapon = NextWeapon(e, +1);
            else if (WeaponsOn && pad.dpad.left.wasPressedThisFrame) weapon = NextWeapon(e, -1);
            else if (SpellsOn && pad.dpad.up.wasPressedThisFrame) spell = NextSpell(e, +1);
            else if (SpellsOn && pad.dpad.down.wasPressedThisFrame) spell = NextSpell(e, -1);
            else return;

            if (weapon != null)
            {
                pendingWeapon = weapon;
                pendingSpell = null;
            }
            else if (spell != null)
            {
                pendingSpell = spell;
                pendingWeapon = null;
            }

            var actor = Player.Comp<Actor>(e);
            if (actor != null) TryFlush(e, actor);
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static void TryFlush(Entity e, Actor actor)
    {
        if (Busy(actor)) return;
        var inv = Player.Inventory(e);
        if (inv == null) return;
        if (pendingWeapon != null)
        {
            var weapon = pendingWeapon;
            pendingWeapon = null;
            inv.EquipWeaponFromInventory(weapon);
            Log.Debug($"QuickEquip: weapon -> {weapon.name} ({weapon.itemID}, {Family(weapon)})");
        }
        else if (pendingSpell != null)
        {
            var spell = pendingSpell;
            pendingSpell = null;
            inv.EquipSpellFromInventory(spell);
            Log.Debug($"QuickEquip: spell -> {spell.name} ({spell.itemID})");
        }
    }

    /// <summary>
    /// Weapon attacks block until they end or reach <see cref="ActionData.chainableFrame"/> (the cancel / combo gap).
    /// Locomotion and other non-weapon actions do not defer the swap.
    /// </summary>
    private static bool Busy(Actor actor)
    {
        var list = actor.activeActions;
        for (var i = 0; i < list.Count; i++)
        {
            var instance = list[i];
            var data = instance.data;
            if (data == null || !ActionInfo.IsAttack(data)) continue;
            var chain = data.chainableFrame;
            if (chain > 0 && instance.frameInfo.integerFrame >= chain) continue;
            return true;
        }
        return false;
    }

    /// <summary>Stick keeps moving; gamepad dpad alone must not.</summary>
    [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.ProcessMoveInputActionBinding)), HarmonyPostfix]
    private static void NoDpadMove(PlayerInput __instance)
    {
        if (!SuppressMove || !Player.Owns(__instance)) return;
        try
        {
            var pad = Gamepad.current;
            if (pad == null) return;
            if (pad.leftStick.ReadValue().sqrMagnitude >= StickDeadzone * StickDeadzone) return;
            if (pad.dpad.ReadValue().sqrMagnitude < 0.25f) return;
            __instance.moveInputValue = Vector2.zero;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static WeaponItemData? NextWeapon(Entity e, int delta)
    {
        var list = BestWeapons(e);
        if (list.Count == 0) return null;
        var current = pendingWeapon ?? Player.Inventory(e)?.GetEquippedWeapon();
        var i = current == null ? -1 : list.FindIndex(w => Family(w) == Family(current));
        if (i < 0) i = delta > 0 ? -1 : 0;
        return list[(i + delta + list.Count * 4) % list.Count];
    }

    private static SpellItemData? NextSpell(Entity e, int delta)
    {
        var list = FreeSpells(e);
        if (list.Count == 0) return null;
        var current = pendingSpell ?? Player.Inventory(e)?.GetEquippedSpell();
        var i = current == null ? -1 : list.FindIndex(s => s.Pointer == current.Pointer);
        if (i < 0) i = delta > 0 ? -1 : 0;
        return list[(i + delta + list.Count * 4) % list.Count];
    }

    /// <summary>Highest owned tier of each family, in pause-menu order. <c>parentWeapon</c> is often unloaded, so the family comes from the asset name.</summary>
    private static List<WeaponItemData> BestWeapons(Entity e)
    {
        var owned = OwnedWeapons(e);
        return owned
            .GroupBy(Family)
            .Select(g => g.OrderByDescending(TierOf).ThenByDescending(w => w.name, StringComparer.Ordinal).First())
            .OrderBy(w =>
            {
                var i = Array.IndexOf(WeaponFamilies, Family(w));
                return i >= 0 ? i : 100;
            })
            .ThenBy(w => w.itemID, StringComparer.Ordinal)
            .ToList();
    }

    private static List<SpellItemData> FreeSpells(Entity e)
    {
        var owned = new List<SpellItemData>();
        var inv = Player.Inventory(e);
        if (inv == null) return owned;
        var equipped = inv.GetEquippedSpell();
        if (equipped != null && !TarotShortcuts.HasQuickCast(equipped)) owned.Add(equipped);
        var items = inv.GetAllItemsInInventory();
        for (var i = 0; i < items.Count; i++)
        {
            var spell = items[i]?.TryCast<SpellItemData>();
            if (spell == null || TarotShortcuts.HasQuickCast(spell)) continue;
            if (!owned.Any(s => s.Pointer == spell.Pointer)) owned.Add(spell);
        }
        return owned
            .OrderBy(s =>
            {
                var i = Array.FindIndex(SpellOrder, k => TarotShortcuts.Matches(s, k));
                return i >= 0 ? i : 100;
            })
            .ThenBy(s => s.itemID, StringComparer.Ordinal)
            .ToList();
    }

    private static List<WeaponItemData> OwnedWeapons(Entity e)
    {
        var owned = new List<WeaponItemData>();
        var inv = Player.Inventory(e);
        if (inv == null) return owned;
        var equipped = inv.GetEquippedWeapon();
        if (equipped != null) owned.Add(equipped);
        var items = inv.GetAllItemsInInventory();
        for (var i = 0; i < items.Count; i++)
        {
            var w = items[i]?.TryCast<WeaponItemData>();
            if (w != null && !owned.Any(x => x.Pointer == w.Pointer)) owned.Add(w);
        }
        return owned;
    }

    private static string Family(WeaponItemData w)
    {
        var id = w.itemID ?? w.name ?? "";
        foreach (var family in WeaponFamilies)
            if (id.StartsWith(family, StringComparison.OrdinalIgnoreCase)) return family;
        return id;
    }

    private static int TierOf(WeaponItemData w)
    {
        var m = Tier.Match(w.itemID ?? w.name ?? "");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }
}
