using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UltraMode.Menu;

/// <summary>
/// Belmont Minimap's MODS pages only scroll to the selected row when the navigate input is still held in the frame
/// it notices the new selection and the mouse is idle, so quick taps can leave the selection below the view. A row
/// selected by keys or pad that is not fully visible is scrolled into view here, with the game's own helper.
/// </summary>
internal static class ScrollFollow
{
    private static readonly string[] ScrollNames = { "ModSettingsScroll", "BelmontMinimap_Scroll" };
    private const float Margin = 30f;
    private static IntPtr lastSelection;
    private static bool failed;

    internal static void Update()
    {
        if (failed) return;
        try
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null)
            {
                lastSelection = IntPtr.Zero;
                return;
            }
            if (selected.Pointer == lastSelection) return;
            lastSelection = selected.Pointer;
            if (PointerActive()) return;

            var scroll = selected.GetComponentInParent<ScrollRect>();
            if (scroll == null || !ScrollNames.Contains(scroll.name) || scroll.content == null || scroll.viewport == null) return;
            var row = RowOf(selected.transform, scroll.content);
            if (row == null || Visible(scroll.viewport, row)) return;
            NavigationUIHelper.AutoScrollRectToItem(scroll, row, Margin);
        }
        catch (Exception e)
        {
            failed = true;
            Plugin.Logger.LogWarning($"Menu scroll follow disabled: {e.Message}");
        }
    }

    /// <summary>The direct child of the scroll content holding the selection: the whole row, not its control.</summary>
    private static RectTransform? RowOf(Transform t, RectTransform content)
    {
        while (t.parent != null && t.parent.Pointer != content.Pointer) t = t.parent;
        return t.parent == null ? null : t.TryCast<RectTransform>();
    }

    private static bool Visible(RectTransform viewport, RectTransform row)
    {
        var bottom = viewport.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMin, 0f))).y;
        var top = viewport.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMax, 0f))).y;
        return bottom >= viewport.rect.yMin - 1f && top <= viewport.rect.yMax + 1f;
    }

    /// <summary>Hovering a row also selects it; scrolling then would pull the list away from the cursor.</summary>
    private static bool PointerActive()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        return mouse.delta.ReadValue().sqrMagnitude > 0.01f || mouse.scroll.ReadValue().sqrMagnitude > 0.01f
            || mouse.leftButton.isPressed || mouse.rightButton.isPressed;
    }
}
