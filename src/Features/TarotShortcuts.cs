using HarmonyLib;
using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = MotherBase.ActionFramework.PlayerInput;

namespace UltraMode.Features;

/// <summary>
/// Y goes through a UseItem trigger that asks the equipped spell's bindings for its action. With LT held, or the
/// stick held up or down, that lookup answers with another owned spell's action instead, and R3 starts the same trigger with
/// the R3 spell forced, so nothing is re-equipped. Mana and health costs ask their feature for the equipped spell;
/// while a substituted spell starts or runs, they get that spell, so each card costs what it costs in vanilla.
/// Spells are picked by keywords matched against the spell's asset name and its bound action names.
/// </summary>
[Feature("TarotShortcuts")]
[HarmonyPatch]
internal static class TarotShortcuts
{
    private static readonly Feature Fix = Feature.Get("TarotShortcuts");
    private const int R3Buffer = 10;

    internal static bool Active => Fix.Enabled && Settings.On && Settings.TarotShortcuts.Value;

    private static readonly List<SpellItemData> owned = new();
    private static int nextOwnedScan;

    /// <summary>The spell trigger Y last went through; R3 starts it.</summary>
    private static UseItem? spellTrigger;
    private static int r3Frame = -1000;
    /// <summary>
    /// The frame Y was pressed with LT held. LT starts a parry first and the game buffers Y until it ends, by which
    /// time LT may be released, so the combo is judged at the press.
    /// </summary>
    private static int ltAtY = -1000;
    private const int LTLatch = 30;
    private static int ltHandled = -1000;
    private static bool starting;
    private static IntPtr suppressData;
    private static int suppressFrame, castEndFrame = -1000;
    private const int SuppressMax = 600;
    /// <summary>Set only while R3 starts the trigger.</summary>
    private static SpellItemData? forced;

    /// <summary>The spell the last lookup substituted, with the action it answered.</summary>
    private static SpellItemData? pending;
    private static IntPtr pendingData;
    private static int pendingFrame = -1000;
    private static int lookupLogFrame = -1000;

    /// <summary>A substituted spell whose actions are running: its stem is the first token of the started action's name.</summary>
    private static SpellItemData? casting;
    private static string castStem = "";
    private static int castFrame;

    internal static void Init()
    {
        ActionTracker.Started += (e, data) =>
        {
            if (!Fix.Enabled || pending == null || data.Pointer != pendingData || Time.frameCount - pendingFrame > 2) return;
            casting = pending;
            castStem = Stem(data.name);
            castFrame = Time.frameCount;
            Log.Debug($"TarotShortcuts: casting {casting.name} via {data.name}");
        };
        ActionTracker.Updated += (e, actor) =>
        {
            if (!Fix.Enabled) return;
            try { Update(e, actor); }
            catch (Exception ex) { Fix.Fail(ex); }
        };
    }

    /// <summary>Called every frame from the plugin's behaviour, which sees button presses on the frame they happen.</summary>
    internal static void PollInput()
    {
        if (!Active) return;
        var pad = Gamepad.current;
        if (pad == null) return;
        var rb = pad.rightShoulder.isPressed;
        if (pad.rightStickButton.wasPressedThisFrame)
        {
            r3Frame = Time.frameCount;
            rbAtR3 = rb;
        }
        if (pad.buttonNorth.wasPressedThisFrame)
        {
            ltAtY = pad.leftTrigger.ReadValue() > 0.5f ? Time.frameCount : -1000;
            rbAtY = rb ? Time.frameCount : -1000;
        }
    }

    /// <summary>RB is held when Y or R3 is pressed; judged at the press like LT, since the cast may start frames later.</summary>
    private static int rbAtY = -1000;
    private static bool rbAtR3;

    private static void Update(Entity e, Actor actor)
    {
        if (casting != null && Time.frameCount > castFrame + 2 && !ActionTracker.IsActive(d => Stem(d.name) == castStem))
        {
            Log.Debug($"TarotShortcuts: {casting.name} ended");
            casting = null;
            viaR3 = false;
            castEndFrame = Time.frameCount;
        }
        if (!Active) return;
        if (ltAtY > ltHandled)
        {
            ltHandled = ltAtY;
            ForceOverParry(e, actor);
        }
        if (Time.frameCount - r3Frame > R3Buffer) return;
        var combo = rbAtR3 ? "RB+R3" : "R3";
        var spell = Resolve(e, rbAtR3 ? Settings.TarotRBR3Spells : Settings.TarotR3Spells);
        var trigger = spellTrigger ?? FindSpellTrigger();
        if (spell == null || trigger == null)
        {
            Log.Debug($"TarotShortcuts: {combo} ignored ({(spell == null ? "no owned spell for it" : "no spell trigger")})");
            r3Frame = -1000;
            return;
        }
        forced = spell;
        try
        {
            if (!actor.TryStartAction(e, trigger)) return;
        }
        finally { forced = null; }
        Log.Debug($"TarotShortcuts: {combo} started {spell.name}");
        r3Frame = -1000;
        viaR3 = true;
        viaR3Frame = Time.frameCount;
    }

    /// <summary>A spell R3 started: holding R3 stands in for holding the spell button (charged or held spells).</summary>
    private static bool viaR3;
    private static int viaR3Frame;
    private const string SpellInput = "Player_Spell_InputState";

    [HarmonyPatch(typeof(InputConditionFeature), nameof(InputConditionFeature.IsOtherInputOk)), HarmonyPostfix]
    private static void R3HoldInput(InputConditionFeature __instance, Entity e, ref bool __result) => R3Hold(__instance, e, ref __result, "IsOtherInputOk");

    [HarmonyPatch(typeof(InputConditionFeature), nameof(InputConditionFeature.IsConditionValid)), HarmonyPostfix]
    private static void R3HoldCondition(InputConditionFeature __instance, Entity e, ref bool __result) => R3Hold(__instance, e, ref __result, "IsConditionValid");

    private static int holdLogFrame = -1000;

    private static void R3Hold(InputConditionFeature feature, Entity e, ref bool result, string check)
    {
        if (result || !viaR3 || !Fix.Enabled) return;
        try
        {
            if (casting == null && Time.frameCount > viaR3Frame + 2) { viaR3 = false; return; }
            if (Gamepad.current?.rightStickButton.isPressed != true || !Player.Is(e) || !ChecksSpellInput(feature)) return;
            result = true;
            if (Time.frameCount - holdLogFrame > 30)
            {
                holdLogFrame = Time.frameCount;
                Log.Debug($"TarotShortcuts: R3 held stands in for the spell button ({check})");
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    internal static bool ChecksSpellInput(InputConditionFeature feature)
    {
        var inputs = feature.inputsToCheck;
        if (inputs == null) return false;
        for (var i = 0; i < inputs.Count; i++)
            if (inputs[i]?.name == SpellInput) return true;
        return false;
    }

    /// <summary>The spell's first action keeps running only while the spell button is held.</summary>
    internal static bool IsHeld(SpellItemData spell)
    {
        var bindings = spell.attackBindings;
        if (bindings == null) return false;
        for (var i = 0; i < bindings.Length; i++)
        {
            var data = bindings[i]?.actionTrigger?.TryCast<ActionData>();
            var features = data?.actionFeatures;
            if (features == null) continue;
            for (var j = 0; j < features.Count; j++)
                if (features[j]?.TryCast<InputConditionFeature>() is { } input && input.stopActionIfConditionNoLongerValid && ChecksSpellInput(input)) return true;
        }
        return false;
    }

    /// <summary>
    /// LT has just started a parry, which would hold the spell back until it can chain. When chaining is the only
    /// thing in the way and every feature of the spell's action agrees (mana, spell lock...), it starts now.
    /// </summary>
    private static void ForceOverParry(Entity e, Actor actor)
    {
        var spell = Resolve(e, Settings.TarotLTSpell);
        var trigger = spellTrigger ?? FindSpellTrigger();
        if (spell == null || trigger == null) return;
        forced = spell;
        try
        {
            var data = ActionFor(spell, trigger, e);
            if (data == null) return;
            pending = spell;
            pendingData = data.Pointer;
            pendingFrame = Time.frameCount;
            var result = actor.CheckStartAction(e, data);
            if (result == ActionStartResult.Success) return;
            if (result != ActionStartResult.ChainableIssue || !FeaturesAllow(e, data))
            {
                Log.Debug($"TarotShortcuts: LT+Y waits ({result})");
                return;
            }
            var running = string.Join(", ", ActionTracker.Active.Values.Select(d => d.name));
            starting = true;
            try { actor.StartAction(e, trigger); }
            finally { starting = false; }
            suppressData = data.Pointer;
            suppressFrame = Time.frameCount;
            Log.Debug($"TarotShortcuts: LT+Y started {spell.name} over {running}");
        }
        finally { forced = null; }
    }

    private static bool FeaturesAllow(Entity e, ActionData data)
    {
        var features = data.actionFeatures;
        if (features == null) return true;
        for (var i = 0; i < features.Count; i++)
        {
            var f = features[i];
            if (f != null && !f.CanStart(e, data)) return false;
        }
        return true;
    }

    /// <summary>The Y the game buffered behind the parry must not cast the spell a second time.</summary>
    [HarmonyPatch(typeof(Actor), nameof(Actor.CheckStartAction), typeof(Entity), typeof(ActionData), typeof(int)), HarmonyPostfix]
    private static void CheckStart(Entity e, ActionData action, ref ActionStartResult __result)
    {
        var data = action;
        if (!Fix.Enabled || starting || suppressData == IntPtr.Zero || data == null || data.Pointer != suppressData) return;
        try
        {
            var frame = Time.frameCount;
            if (frame > suppressFrame + SuppressMax || (casting == null && frame > Math.Max(castEndFrame, suppressFrame) + 10))
            {
                suppressData = IntPtr.Zero;
                return;
            }
            if (Player.Is(e)) __result = ActionStartResult.LogicIssue;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static string Stem(string name)
    {
        var i = name.IndexOf('_');
        return i > 0 ? name[..i] : name;
    }

    private static UseItem? FindSpellTrigger()
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<UseItem>()))
        {
            var t = o.TryCast<UseItem>();
            if (t != null && t.itemType == UseItem.UseItemType.Spell && t.name.StartsWith("MC_", StringComparison.Ordinal)) return t;
        }
        return null;
    }

    private static List<SpellItemData> Owned(Entity e)
    {
        if (Time.frameCount < nextOwnedScan) return owned;
        nextOwnedScan = Time.frameCount + 60;
        owned.Clear();
        var inventory = Player.Inventory(e);
        if (inventory == null) return owned;
        var equipped = inventory.GetEquippedSpell();
        if (equipped != null) owned.Add(equipped);
        var items = inventory.GetAllItemsInInventory();
        for (var i = 0; i < items.Count; i++)
        {
            var spell = items[i]?.TryCast<SpellItemData>();
            if (spell != null && !owned.Any(s => s.Pointer == spell.Pointer)) owned.Add(spell);
        }
        return owned;
    }

    /// <summary>The first keyword with an owned spell wins, so an upgraded card listed first replaces its base form.</summary>
    private static SpellItemData? Resolve(Entity e, BepInEx.Configuration.ConfigEntry<string> keywords)
    {
        var spells = Owned(e);
        foreach (var keyword in Settings.Keywords(keywords))
            foreach (var spell in spells)
                if (Matches(spell, keyword)) return spell;
        return null;
    }

    internal static bool Matches(SpellItemData spell, string keyword)
    {
        if (spell.name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        var bindings = spell.attackBindings;
        if (bindings == null) return false;
        for (var i = 0; i < bindings.Length; i++)
        {
            var name = bindings[i]?.actionTrigger?.name;
            if (name != null && name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>True when this card is listed on any Quick Cast combo (even if Quick Cast is currently off).</summary>
    internal static bool HasQuickCast(SpellItemData spell) =>
        new[]
        {
            Settings.TarotUpSpell, Settings.TarotDownSpell, Settings.TarotLTSpell, Settings.TarotR3Spells,
            Settings.TarotRBSpell, Settings.TarotRBUpSpell, Settings.TarotRBDownSpell, Settings.TarotRBR3Spells,
        }.Any(entry => Settings.Keywords(entry).Any(k => Matches(spell, k)));

    private static SpellItemData? Pick(Entity e)
    {
        if (forced != null) return forced;
        if (casting != null) return casting;
        if (Time.frameCount - ltAtY <= LTLatch) return Resolve(e, Settings.TarotLTSpell);
        var input = Player.Comp<PlayerInput>(e);
        if (input == null) return null;
        var move = input.moveInputValue;
        var up = move.y > 0.5f && move.y > Mathf.Abs(move.x);
        var down = move.y < -0.5f && -move.y > Mathf.Abs(move.x);
        if (Time.frameCount - rbAtY <= LTLatch)
            return (up ? Resolve(e, Settings.TarotRBUpSpell) : down ? Resolve(e, Settings.TarotRBDownSpell) : null) ?? Resolve(e, Settings.TarotRBSpell);
        if (up) return Resolve(e, Settings.TarotUpSpell);
        if (down) return Resolve(e, Settings.TarotDownSpell);
        return null;
    }

    /// <summary>The binding for the same trigger, else the spell's first binding.</summary>
    private static ActionData? ActionFor(SpellItemData spell, UseItem trigger, Entity e)
    {
        var bindings = spell.attackBindings;
        if (bindings == null) return null;
        ActionTrigger? fallback = null;
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b?.actionTrigger == null) continue;
            if (b.bindingType != null && b.bindingType.Pointer == trigger.Pointer) return b.actionTrigger.GetActionData(e);
            fallback ??= b.actionTrigger;
        }
        return fallback?.GetActionData(e);
    }

    [HarmonyPatch(typeof(UseItem), nameof(UseItem.GetActionData)), HarmonyPostfix]
    private static void GetActionData(UseItem __instance, Entity e, ref ActionData __result)
    {
        if (!Active) return;
        try
        {
            if (__instance.itemType != UseItem.UseItemType.Spell || !Player.Is(e)) return;
            spellTrigger = __instance;
            var spell = Pick(e);
            if (spell == null)
            {
                if (Time.frameCount != pendingFrame) pending = null;
                return;
            }
            var data = ActionFor(spell, __instance, e);
            if (data == null) return;
            if (data.Pointer != __result?.Pointer && Time.frameCount - lookupLogFrame > 30)
            {
                lookupLogFrame = Time.frameCount;
                var move = Player.Comp<PlayerInput>(e)?.moveInputValue ?? Vector2.zero;
                Log.Debug($"TarotShortcuts: {__result?.name ?? "null"} -> {data.name} ({spell.name}{(casting != null ? ", casting" : "")}, move {move.x:0.00},{move.y:0.00})");
            }
            __result = data;
            pending = spell;
            pendingData = data.Pointer;
            pendingFrame = Time.frameCount;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    private static SpellItemData? CostSpell()
    {
        if (casting != null) return casting;
        return pending != null && Time.frameCount - pendingFrame <= 1 ? pending : null;
    }

    [HarmonyPatch(typeof(SpellManaCostFeature), nameof(SpellManaCostFeature.GetEquippedSpell)), HarmonyPostfix]
    private static void ManaSpell(Entity e, ref SpellItemData __result) => CostOverride(e, ref __result);

    [HarmonyPatch(typeof(SpellHealthCostFeature), nameof(SpellHealthCostFeature.GetEquippedSpell)), HarmonyPostfix]
    private static void HealthSpell(Entity e, ref SpellItemData __result) => CostOverride(e, ref __result);

    private static void CostOverride(Entity e, ref SpellItemData __result)
    {
        if (!Fix.Enabled) return;
        try
        {
            if (!Player.Is(e)) return;
            var spell = CostSpell();
            if (spell != null) __result = spell;
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
