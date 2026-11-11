using HarmonyLib;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// Owned charms are equipped once as passives (through the game's own OnEquipItem), and equip/unequip from the
/// menu no longer adds or removes them: charms without drawbacks when PassiveCharms is on, the DrawbackCharms list
/// when PassiveDrawbackCharms is on. Turning an option off unequips its charms again unless they sit in a slot.
/// Charm effects, drawbacks included, are applied exactly as the game defines them.
/// ForceRevealSecret makes the game's equipped checks report the Gloomstone even when it isn't owned; the
/// charm has no affect, the hidden-wall reveal only asks whether it is equipped.
/// The charms menu and the inventory's own equip calls see the real slots for HasCharmEquipped (so a click still
/// means "put into a slot"), while the equipped frame on inventory tiles is lit separately for passives.
/// </summary>
[Feature("PassiveCharms")]
[HarmonyPatch]
internal static class PassiveCharms
{
    private static readonly Feature Fix = Feature.Get("PassiveCharms");
    /// <summary>Charms equipped as passives.</summary>
    private static readonly HashSet<(IntPtr inventory, IntPtr charm)> applied = new();
    private static int nextSync;
    /// <summary>Re-entrancy guard for the HasCharmEquipped postfix.</summary>
    private static bool inHasCharm;
    /// <summary>Above 0 while the inventory's own equip/unequip code runs; it must see the real slots.</summary>
    private static int slotQueries;
    /// <summary>The last charms menu opened; while it is open, equipped checks report the real slots too.</summary>
    private static InventoryCharmsMenuManager? charmsMenu;
    private static string drawbackSource = "";
    private static string[] drawbackCharms = Array.Empty<string>();

    internal static void Init() => ActionTracker.Updated += Sync;

    private static bool Active => Fix.Enabled && Settings.On && (Settings.PassiveCharms.Value || Settings.PassiveDrawbackCharms.Value);

    private static bool MenuOpen => charmsMenu != null && charmsMenu.opened && charmsMenu.isActiveAndEnabled;

    private static bool HasDrawback(CharmItemData charm)
    {
        if (drawbackSource != Settings.DrawbackCharms.Value)
        {
            drawbackSource = Settings.DrawbackCharms.Value;
            drawbackCharms = Settings.Keywords(Settings.DrawbackCharms);
        }
        return drawbackCharms.Contains(charm.name.ToLowerInvariant());
    }

    private static bool IsPassive(CharmItemData charm) =>
        HasDrawback(charm) ? Settings.PassiveDrawbackCharms.Value : Settings.PassiveCharms.Value;

    private static bool InSlot(Inventory inventory, CharmItemData charm)
    {
        var slots = inventory._equippedCharms;
        if (slots == null) return false;
        for (var i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].Pointer == charm.Pointer) return true;
        return false;
    }

    /// <summary>Once a second: owned charms that should be passive are equipped, and ones that no longer should are unequipped.</summary>
    private static void Sync(Entity e, Actor actor)
    {
        if (!Fix.Enabled || !Settings.On || Time.frameCount < nextSync) return;
        nextSync = Time.frameCount + 60;
        try
        {
            var inventory = Player.Inventory(e);
            if (inventory == null) return;
            var items = inventory.GetAllItemsInInventory();
            for (var i = 0; i < items.Count; i++)
            {
                var charm = items[i]?.TryCast<CharmItemData>();
                if (charm == null) continue;
                var key = (inventory.Pointer, charm.Pointer);
                if (!IsPassive(charm))
                {
                    if (applied.Remove(key) && !InSlot(inventory, charm))
                    {
                        charm.OnUnequipItem(e, inventory);
                        Log.Debug($"PassiveCharms: {charm.name} no longer passive");
                    }
                    continue;
                }
                if (applied.Contains(key)) continue;
                charm.OnEquipItem(e, inventory);
                Log.Debug($"PassiveCharms: {charm.name} active");
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(CharmItemData), nameof(CharmItemData.OnEquipItem)), HarmonyPrefix]
    private static bool OnEquip(CharmItemData __instance, Inventory inventory)
    {
        if (!Active) return true;
        try
        {
            if (!IsPassive(__instance)) return true;
            return applied.Add((inventory.Pointer, __instance.Pointer));
        }
        catch (Exception ex) { Fix.Fail(ex); }
        return true;
    }

    [HarmonyPatch(typeof(CharmItemData), nameof(CharmItemData.OnUnequipItem)), HarmonyPrefix]
    private static bool OnUnequip(CharmItemData __instance, Inventory inventory)
    {
        if (!Active) return true;
        return !applied.Contains((inventory.Pointer, __instance.Pointer));
    }

    private static readonly Dictionary<IntPtr, bool> alwaysOnCache = new();

    /// <summary>Charms that are only ever queried (no affect of their own), forced on even when not owned.</summary>
    private static bool AlwaysOn(CharmItemData charm)
    {
        if (!Fix.Enabled || !Settings.On || !Settings.ForceRevealSecret.Value) return false;
        if (!alwaysOnCache.TryGetValue(charm.Pointer, out var on))
            alwaysOnCache[charm.Pointer] = on = charm.name == RevealSecret;
        return on;
    }

    private const string RevealSecret = "Charm_RevealSecret";

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HasCharmEquipped)), HarmonyPostfix]
    private static void HasCharmEquipped(Inventory __instance, CharmItemData charm, ref bool __result)
    {
        if (__result || inHasCharm || slotQueries > 0 || charm == null) return;
        var forced = AlwaysOn(charm);
        if (!forced && !Active) return;
        inHasCharm = true;
        try
        {
            if (MenuOpen) return;
            __result = forced || applied.Contains((__instance.Pointer, charm.Pointer));
            if (forced) LogForced(nameof(Inventory.HasCharmEquipped));
        }
        catch (Exception ex) { Fix.Fail(ex); }
        finally { inHasCharm = false; }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HasItemEquipped), typeof(EquippableItemData)), HarmonyPostfix]
    private static void HasEquippableEquipped(EquippableItemData item, ref bool __result) => ForceItem(item, ref __result);

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HasItemEquipped), typeof(ItemData)), HarmonyPostfix]
    private static void HasItemEquipped(ItemData item, ref bool __result) => ForceItem(item, ref __result);

    private static void ForceItem(ItemData? item, ref bool __result)
    {
        if (__result || item == null || slotQueries > 0) return;
        try
        {
            var charm = item.TryCast<CharmItemData>();
            if (charm == null || !AlwaysOn(charm) || MenuOpen) return;
            __result = true;
            LogForced(nameof(Inventory.HasItemEquipped));
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static readonly HashSet<string> loggedForced = new();

    private static void LogForced(string via)
    {
        if (loggedForced.Add(via)) Log.Debug($"PassiveCharms: {RevealSecret} forced on via {via}");
    }

    [HarmonyPatch(typeof(InventoryCharmsMenuManager), nameof(InventoryCharmsMenuManager.OnOpened)), HarmonyPostfix]
    private static void MenuOpened(InventoryCharmsMenuManager __instance) => charmsMenu = __instance;

    /// <summary>
    /// HasCharmEquipped stays false for passives while the menu is open (so clicks still equip into a slot).
    /// Light the same equipped frame the game uses for slotted charms.
    /// </summary>
    [HarmonyPatch(typeof(InventoryItemSlotController<CharmItemData>), nameof(InventoryItemSlotController<CharmItemData>.SetUpSlot))]
    [HarmonyPostfix]
    private static void SetUpPassiveEquippedFrame(InventoryItemSlotController<CharmItemData> __instance, Inventory inventory, CharmItemData item) =>
        ShowPassiveEquippedFrame(__instance, inventory, item);

    [HarmonyPatch(typeof(InventoryItemSlotController<CharmItemData>), nameof(InventoryItemSlotController<CharmItemData>.RefreshSlot))]
    [HarmonyPostfix]
    private static void RefreshPassiveEquippedFrame(InventoryItemSlotController<CharmItemData> __instance) =>
        ShowPassiveEquippedFrame(__instance, __instance._inventory, __instance.GetItem());

    private static void ShowPassiveEquippedFrame(InventoryItemSlotController<CharmItemData>? slot, Inventory? inventory, CharmItemData? item)
    {
        if (!Active || slot == null || inventory == null || item == null) return;
        try
        {
            if (!applied.Contains((inventory.Pointer, item.Pointer))) return;
            var display = slot.equippedDisplay;
            if (display != null) display.SetBool(true);
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.EquipCharmFromInventory), typeof(CharmItemData), typeof(int)), HarmonyPrefix]
    private static void EquipBegin() => slotQueries++;

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.EquipCharmFromInventory), typeof(CharmItemData), typeof(int)), HarmonyFinalizer]
    private static void EquipEnd() => slotQueries--;

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanEquipCharm)), HarmonyPrefix]
    private static void CanEquipBegin() => slotQueries++;

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanEquipCharm)), HarmonyFinalizer]
    private static void CanEquipEnd() => slotQueries--;

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.UnequipCharm), typeof(CharmItemData)), HarmonyPrefix]
    private static void UnequipBegin() => slotQueries++;

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.UnequipCharm), typeof(CharmItemData)), HarmonyFinalizer]
    private static void UnequipEnd() => slotQueries--;
}
