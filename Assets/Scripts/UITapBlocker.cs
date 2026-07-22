using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

/// <summary>
/// Global "is the pointer over an open menu?" test for world-space tap handlers.
///
/// World props (trees, the wood rack, shops, the cannery) read the raw pointer through the
/// Input System and hit-test their own colliders — which is completely independent of the UI
/// Toolkit panels that popups draw into. So without a guard, a tap on top of an open popup
/// falls through to whatever world object sits behind it (e.g. chopping a tree or clicking the
/// wood rack "through" an open panel).
///
/// This walks every live UITK runtime panel and asks it to Pick() at the pointer: if any
/// pickable element is there, a menu is covering that point and the world tap must be swallowed.
/// Our popups follow the convention of a full-screen pickable backdrop (picking-mode="Position")
/// while open and display:none while closed, so this needs zero per-popup wiring and covers
/// current and future popups automatically. uGUI (bottom nav, drawer) is covered via the
/// EventSystem as a bonus.
/// </summary>
public static class UITapBlocker
{
    private static readonly List<IPanel> panels = new List<IPanel>();
    private static float nextPanelRefresh;

    /// <summary>True when a menu/overlay is drawn over this screen position (bottom-left origin).</summary>
    public static bool PointerOverUI(Vector2 screenPos)
    {
        // uGUI canvases (bottom nav, drawer, any legacy popup).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        RefreshPanels();
        for (int i = 0; i < panels.Count; i++)
        {
            IPanel panel = panels[i];
            if (panel == null) continue;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
            if (panel.Pick(panelPos) != null) return true;
        }
        return false;
    }

    // Panels change rarely (only as UIDocuments enable/disable), so rebuild the cache on a short
    // interval to keep per-frame "held"-state polling cheap.
    private static void RefreshPanels()
    {
        if (panels.Count > 0 && Time.unscaledTime < nextPanelRefresh) return;
        nextPanelRefresh = Time.unscaledTime + 0.5f;

        panels.Clear();
        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in docs)
        {
            if (doc == null || doc.rootVisualElement == null) continue;
            IPanel panel = doc.rootVisualElement.panel;
            if (panel != null && !panels.Contains(panel)) panels.Add(panel);
        }
    }
}
