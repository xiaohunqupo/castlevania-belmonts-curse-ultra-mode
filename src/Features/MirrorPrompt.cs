using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace UltraMode.Features;

/// <summary>
/// Mirror rooms only prompt the interact button, although crouching at the mirror also enters it. While the player
/// stands at an ordinary mirror, the interact prompt's tagged source text gets a copy of itself pointing at the
/// "down" part of the move action, and the game's own preprocessor turns both tags into device-specific icons.
/// </summary>
[Feature("MirrorPrompt")]
internal static class MirrorPrompt
{
    private static readonly Feature Fix = Feature.Get("MirrorPrompt");
    private const string Separator = "  /  ";
    private const int ScanInterval = 30;
    private const float FallbackRadius = 3f;
    private static readonly Regex InputTag = new("<input=\"([^\"]+)\">", RegexOptions.Compiled);

    private sealed class Prompt
    {
        internal readonly InputTagTextPreprocessor Tags;
        internal string? Source, Applied;
        internal Prompt(InputTagTextPreprocessor tags) => Tags = tags;
    }

    private static readonly List<(GPE_Mirror mirror, Transform point, float radius)> mirrors = new();
    private static readonly List<Prompt> prompts = new();
    private static readonly HashSet<IntPtr> reported = new();
    /// <summary>Mirrors whose own check threw; one failing mirror must not turn the prompt off everywhere.</summary>
    private static readonly HashSet<IntPtr> failing = new();
    private static readonly Dictionary<IntPtr, string?> downTags = new();
    private static int nextScan;
    private static bool wasNear;

    internal static void Init()
    {
        ActionTracker.Updated += (e, _) =>
        {
            if (!Fix.Enabled) return;
            try { Update(e); }
            catch (Exception ex) { Fix.Fail(ex); }
        };
        Settings.OnChanged(RestoreAll, Settings.Enabled, Settings.MirrorPrompt);
    }

    private static void Update(Entity e)
    {
        var scan = Time.frameCount >= nextScan;
        if (scan)
        {
            nextScan = Time.frameCount + ScanInterval;
            ScanMirrors();
        }
        var near = Settings.On && Settings.MirrorPrompt.Value && NearMirror(e);
        if (!near)
        {
            if (wasNear) RestoreAll();
            wasNear = false;
            return;
        }
        wasNear = true;
        if (scan)
        {
            ScanPrompts();
            if (prompts.Count == 0 && reported.Add(IntPtr.Zero)) Log.Debug("MirrorPrompt: at a mirror but no tagged interact prompt found");
        }
        foreach (var p in prompts)
        {
            if (p.Tags == null || p.Tags.WasCollected || !p.Tags.gameObject.activeInHierarchy) continue;
            var text = p.Tags.text;
            if (text == null || (p.Applied != null && text.text == p.Applied)) continue;
            Apply(p);
        }
    }

    /// <summary>The source is re-read every time: localization or a device change rewrites both it and the text.</summary>
    private static void Apply(Prompt p)
    {
        var current = p.Tags._initialText;
        if (string.IsNullOrEmpty(current)) return;
        if (p.Source == null || !current.StartsWith(p.Source, StringComparison.Ordinal) || current == p.Source) p.Source = current;
        var match = InputTag.Match(p.Source);
        if (!match.Success) return;
        var down = DownTag(p.Tags, match.Groups[1].Value);
        if (down == null) return;

        var copy = p.Source.Remove(match.Groups[1].Index, match.Groups[1].Length).Insert(match.Groups[1].Index, down);
        var combined = p.Source + Separator + copy;
        p.Tags._initialText = combined;
        p.Applied = p.Tags.PreprocessText(combined);
        p.Tags.text.text = p.Applied;
        if (reported.Add(p.Tags.Pointer)) Log.Debug($"MirrorPrompt: '{p.Source}' -> '{combined}'");
    }

    /// <summary>
    /// The tag syntax for a composite part is not visible in data, so a few spellings derived from the interact tag
    /// are offered to the game's own parser and the first one it resolves to a "down" part wins.
    /// </summary>
    private static string? DownTag(InputTagTextPreprocessor tags, string interactValue)
    {
        var asset = tags.inputAsset;
        if (asset == null) return null;
        if (downTags.TryGetValue(asset.Pointer, out var cached)) return cached;

        string? found = null;
        var slash = interactValue.LastIndexOf('/');
        var map = slash >= 0 ? interactValue[..slash] : "";
        foreach (var actionMap in asset.actionMaps)
        {
            if (found != null) break;
            if (map.Length > 0 && !string.Equals(actionMap.name, map, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var action in actionMap.actions)
            {
                if (found != null) break;
                string? composite = null, part = null;
                foreach (var b in action.bindings)
                {
                    if (b.isComposite) composite = b.name;
                    else if (b.isPartOfComposite && string.Equals(b.name, "down", StringComparison.OrdinalIgnoreCase)) { part = b.name; break; }
                }
                if (part == null) continue;
                var prefix = map.Length > 0 ? map + "/" : "";
                foreach (var candidate in Candidates(prefix + action.name, composite, part))
                {
                    try
                    {
                        var resolved = tags.GetActionFromInputTag(candidate, out var compositeName, out _);
                        if (resolved == null || resolved.Pointer != action.Pointer) continue;
                        if (compositeName == null || !compositeName.Contains(part, StringComparison.OrdinalIgnoreCase)) continue;
                        found = candidate;
                        break;
                    }
                    catch { }
                }
            }
        }
        downTags[asset.Pointer] = found;
        Log.Debug(found != null ? $"MirrorPrompt: down tag = <input=\"{found}\">" : $"MirrorPrompt: no down tag accepted (interact tag '{interactValue}')");
        return found;
    }

    private static IEnumerable<string> Candidates(string action, string? composite, string part)
    {
        foreach (var p in new[] { part, part.ToLowerInvariant(), char.ToUpperInvariant(part[0]) + part[1..] })
        {
            foreach (var sep in new[] { ":", "/", ".", "#", "|" }) yield return action + sep + p;
            if (composite != null)
                foreach (var sep in new[] { ":", "/", "." }) yield return action + sep + composite + sep + p;
        }
    }

    private static void ScanMirrors()
    {
        mirrors.Clear();
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<GPE_Mirror>()))
        {
            var m = o.TryCast<GPE_Mirror>();
            if (m == null || m.mirrorType != MirrorType.BaseMirror) continue;
            var interactible = m.GetComponentInChildren<Interactible>();
            if (interactible != null)
                mirrors.Add((m, interactible.transform, interactible.interactRadius + 0.5f));
            else
                mirrors.Add((m, m.transform, FallbackRadius));
        }
    }

    /// <summary>Only a mirror that can teleport right now: an unused save room's mirror only saves.</summary>
    private static bool NearMirror(Entity e)
    {
        var position = e.transform.position;
        foreach (var (mirror, point, radius) in mirrors)
        {
            if (point == null || point.WasCollected || !point.gameObject.activeInHierarchy || Vector3.Distance(point.position, position) > radius) continue;
            if (mirror == null || mirror.WasCollected || failing.Contains(mirror.Pointer)) continue;
            try
            {
                if (mirror.IsATeleportMirror && mirror.CanUseMirror(e)) return true;
            }
            catch (Exception ex)
            {
                failing.Add(mirror.Pointer);
                Log.Debug($"MirrorPrompt: {Path(mirror.transform)} can't be checked, skipped: {ex.Message}");
            }
        }
        return false;
    }

    private static string Path(Transform t) => t.parent == null ? $"{t.gameObject.scene.name}:{t.name}" : $"{Path(t.parent)}/{t.name}";

    private static void ScanPrompts()
    {
        prompts.RemoveAll(p => p.Tags == null || p.Tags.WasCollected);
        var known = new HashSet<IntPtr>(prompts.Select(p => p.Tags.Pointer));
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<InputTagTextPreprocessor>()))
        {
            var t = o.TryCast<InputTagTextPreprocessor>();
            if (t == null || known.Contains(t.Pointer) || !IsInteractPrompt(t._initialText)) continue;
            prompts.Add(new Prompt(t));
        }
    }

    private static bool IsInteractPrompt(string? source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        foreach (Match m in InputTag.Matches(source))
            if (m.Groups[1].Value.Contains("Interact", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void RestoreAll()
    {
        try
        {
            foreach (var p in prompts)
            {
                if (p.Tags == null || p.Tags.WasCollected || p.Source == null) continue;
                if (p.Applied != null && p.Tags._initialText != p.Source)
                {
                    p.Tags._initialText = p.Source;
                    if (p.Tags.text != null) p.Tags.text.text = p.Tags.PreprocessText(p.Source);
                }
                p.Applied = null;
            }
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
