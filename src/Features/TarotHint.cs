using System.Text.RegularExpressions;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UltraMode.Features;

/// <summary>
/// On the spell page, a card with a quick-cast shortcut gets a line appended to its description naming the buttons.
/// The buttons are written as the game's input tags and turned into icons by one of the page's own
/// InputTagTextPreprocessors, so they follow the device and the player's bindings. The description is filled in
/// asynchronously by localization, so the line is re-appended whenever the text no longer ends with it.
/// </summary>
[Feature("TarotShortcuts")]
[HarmonyPatch]
internal static class TarotHint
{
    private static readonly Feature Fix = Feature.Get("TarotShortcuts");
    private static readonly Dictionary<(IntPtr, string), string?> tags = new();

    private static TMP_Text? target;
    private static string hint = "";
    private static TMP_SpriteAsset? sprites;
    private static InventorySpellMenuManager? menu;
    private static SpellItemData? shown;
    private static bool dirty;

    private static void Init() => Plugin.Instance.Config.SettingChanged += (_, _) => dirty = true;

    [HarmonyPatch(typeof(InventorySpellMenuManager), nameof(InventorySpellMenuManager.RefreshSelectedItemDisplay)), HarmonyPostfix]
    private static void Refreshed(InventorySpellMenuManager __instance, SpellItemData __0)
    {
        if (!Fix.Enabled) return;
        try
        {
            menu = __instance;
            shown = __0;
            Clear();
            var keys = TarotShortcuts.Active && __0 != null ? Keys(__0) : null;
            if (keys == null) return;
            var text = Description(__instance);
            if (text == null) { Log.Debug($"TarotShortcuts: no description text for {__0!.name}"); return; }
            var pre = Preprocessor(__instance);
            var zh = Application.systemLanguage is SystemLanguage.Chinese or SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional
                     || text.text.Any(c => c >= '\u4e00' && c <= '\u9fff');
            var buttons = string.Join(" + ", keys.Select(k => Button(pre, k) ?? Fallback(k, zh)));
            var held = TarotShortcuts.IsHeld(__0!);
            var source = zh ? $"{(held ? "按住" : "按下")} {buttons} 快速施法" : $"{(held ? "Hold" : "Press")} {buttons} to quick cast";
            target = text;
            hint = pre != null ? pre.PreprocessText(source) : source;
            sprites = pre?.text?.spriteAsset;
            Tick();
            Log.Debug($"TarotShortcuts: hint for {__0!.name} on {text.name}: {source} -> {hint}");
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    /// <summary>Called every frame from the plugin's behaviour. A settings change redoes the shown card's line, or removes it.</summary>
    internal static void Tick()
    {
        if (dirty)
        {
            dirty = false;
            if (menu != null && !menu.WasCollected && shown != null && !shown.WasCollected && menu.gameObject.activeInHierarchy)
                Refreshed(menu, shown);
            else Clear();
        }
        if (target == null) return;
        try
        {
            if (target.WasCollected || !target.gameObject.activeInHierarchy) { target = null; return; }
            var current = target.text ?? "";
            if (current.Length == 0 || current.EndsWith(hint, StringComparison.Ordinal)) return;
            if (sprites != null && target.spriteAsset == null) target.spriteAsset = sprites;
            target.text = Strip(current) + "\n" + hint;
        }
        catch (Exception ex) { target = null; Fix.Fail(ex); }
    }

    private static void Clear()
    {
        if (target != null && !target.WasCollected && hint.Length > 0 && target.text?.Contains(hint, StringComparison.Ordinal) == true)
            target.text = Strip(target.text);
        target = null;
        hint = "";
    }

    private static string Strip(string text)
    {
        if (hint.Length == 0) return text;
        var i = text.IndexOf("\n" + hint, StringComparison.Ordinal);
        return i >= 0 ? text[..i] : text;
    }

    /// <summary>The last filled text of the selected card's display: the secondary description when shown, else the description.</summary>
    private static TMP_Text? Description(InventorySpellMenuManager menu)
    {
        var display = menu.selectedItemDisplay?.TryCast<InventoryItemDisplayBase>();
        if (display == null)
            foreach (var d in menu.GetComponentsInChildren<InventoryItemDisplayBase>(false))
                if (d.itemDescriptionText != null) { display = d; break; }
        if (display == null) return null;
        TMP_Text? Text(UILocalizedString? s) => s == null ? null : s.textDisplay ?? s.GetComponent<TMP_Text>();
        var secondary = Text(display.itemSecondaryDescritpionText);
        if (secondary != null && secondary.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(secondary.text)) return secondary;
        return Text(display.itemDescriptionText);
    }

    private enum Key { Up, Down, Y, LT, RB, R3 }

    private static Key[]? Keys(SpellItemData spell)
    {
        bool In(BepInEx.Configuration.ConfigEntry<string> entry) => Settings.Keywords(entry).Any(k => TarotShortcuts.Matches(spell, k));
        if (In(Settings.TarotLTSpell)) return new[] { Key.LT, Key.Y };
        if (In(Settings.TarotRBUpSpell)) return new[] { Key.RB, Key.Up, Key.Y };
        if (In(Settings.TarotRBDownSpell)) return new[] { Key.RB, Key.Down, Key.Y };
        if (In(Settings.TarotRBSpell)) return new[] { Key.RB, Key.Y };
        if (In(Settings.TarotRBR3Spells)) return new[] { Key.RB, Key.R3 };
        if (In(Settings.TarotUpSpell)) return new[] { Key.Up, Key.Y };
        if (In(Settings.TarotDownSpell)) return new[] { Key.Down, Key.Y };
        if (In(Settings.TarotR3Spells)) return new[] { Key.R3 };
        return null;
    }

    /// <summary>
    /// Y and the stick follow the player's bindings through input tags. LT, RB and R3 are read straight from the pad, so
    /// they show that pad button's own sprite, named like the Y sprite with the control path swapped.
    /// </summary>
    private static string? Button(InputTagTextPreprocessor? pre, Key key)
    {
        if (pre == null) return null;
        if (key is not (Key.LT or Key.RB or Key.R3)) return Tag(pre, key) is { } t ? $"<input=\"{t}\">" : null;
        if (Tag(pre, Key.Y) is not { } y) return null;
        var north = Regex.Match(pre.PreprocessText($"<input=\"{y}\">"), "sprite name=\"([^\"]*?)buttonNorth\"");
        if (!north.Success) return null;
        var asset = pre.text?.spriteAsset ?? TMP_Settings.defaultSpriteAsset;
        var controls = key switch
        {
            Key.LT => new[] { "leftTrigger" },
            Key.RB => new[] { "rightShoulder" },
            _ => new[] { "rightStickPress", "rightStickButton", "rightStick/press", "rightStick" },
        };
        foreach (var control in controls)
        {
            var name = north.Groups[1].Value + control;
            if (asset != null && TMP_SpriteAsset.SearchForSpriteByHashCode(asset, TMP_TextUtilities.GetSimpleHashCode(name), true, out _) == null) continue;
            Log.Debug($"TarotShortcuts: {key} sprite = {name}");
            return $"<style=\"InputTag\"><sprite name=\"{name}\"></style>";
        }
        Log.Debug($"TarotShortcuts: no {key} sprite with prefix {north.Groups[1].Value}");
        return null;
    }

    private static string Fallback(Key key, bool zh) => key switch
    {
        Key.Up => zh ? "上" : "Up",
        Key.Down => zh ? "下" : "Down",
        _ => key.ToString(),
    };

    private static InputTagTextPreprocessor? Preprocessor(InventorySpellMenuManager menu)
    {
        foreach (var p in menu.GetComponentsInChildren<InputTagTextPreprocessor>(true))
            if (p.inputAsset != null) return p;
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<InputTagTextPreprocessor>()))
        {
            var p = o.TryCast<InputTagTextPreprocessor>();
            if (p != null && p.inputAsset != null && p.gameObject.scene.IsValid()) return p;
        }
        Log.Debug("TarotShortcuts: no input tag preprocessor, hint without icons");
        return null;
    }

    /// <summary>The tag naming the action behind a button, accepted by the game's own parser; null if none is bound.</summary>
    private static string? Tag(InputTagTextPreprocessor pre, Key key)
    {
        var asset = pre.inputAsset;
        var cacheKey = (asset.Pointer, key.ToString());
        if (tags.TryGetValue(cacheKey, out var cached)) return cached;
        string? found = null;
        if (key is Key.Up or Key.Down)
        {
            var part = key == Key.Up ? "up" : "down";
            if (Accepts(pre, $"Move/{part}", part)) found = $"Move/{part}";
        }
        else
        {
            var (nameHint, path) = key switch
            {
                Key.Y => ("Spell", "<Gamepad>/buttonNorth"),
                Key.LT => ("Parry", "<Gamepad>/leftTrigger"),
                _ => ("", "<Gamepad>/rightStickPress"),
            };
            InputAction? byName = null, byPath = null;
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                {
                    if (nameHint.Length > 0 && byName == null && action.name.Contains(nameHint, StringComparison.OrdinalIgnoreCase)) byName = action;
                    if (byPath != null) continue;
                    foreach (var b in action.bindings)
                        if (string.Equals(b.effectivePath, path, StringComparison.OrdinalIgnoreCase) || string.Equals(b.path, path, StringComparison.OrdinalIgnoreCase)) { byPath = action; break; }
                }
            foreach (var action in new[] { byName, byPath })
            {
                if (action == null || found != null) continue;
                foreach (var candidate in new[] { action.name, $"{action.actionMap?.name}/{action.name}" })
                    if (Accepts(pre, candidate, null)) { found = candidate; break; }
            }
        }
        tags[cacheKey] = found;
        Log.Debug($"TarotShortcuts: {key} tag = {found ?? "none"}");
        return found;
    }

    private static bool Accepts(InputTagTextPreprocessor pre, string tag, string? part)
    {
        try
        {
            var action = pre.GetActionFromInputTag(tag, out var composite, out _);
            if (action == null) return false;
            return part == null || (composite != null && composite.Contains(part, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }
}
