using UnityEngine;
using UnityEditor;

/// <summary>
/// Color-codes GameObjects in the Hierarchy window by category.
/// Draws a colored bar and label behind each item for easy visual scanning.
/// To disable: uncheck "Hierarchy Colorizer" in the Editor menu.
/// </summary>
[InitializeOnLoad]
public static class HierarchyColorizer
{
    private static bool enabled = true;

    static HierarchyColorizer()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    [MenuItem("Tools/Toggle Hierarchy Colorizer")]
    private static void ToggleColorizer()
    {
        enabled = !enabled;
        EditorApplication.RepaintHierarchyWindow();
        Debug.Log($"Hierarchy Colorizer: {(enabled ? "ON" : "OFF")}");
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        if (!enabled) return;

        GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (go == null) return;

        Color color = GetCategoryColor(go, out string tag);
        if (color == Color.clear) return;

        // Draw colored background bar (subtle, left side)
        Rect barRect = new Rect(selectionRect.x - 2, selectionRect.y, 3, selectionRect.height);
        EditorGUI.DrawRect(barRect, color);

        // Draw subtle background tint
        Color bgTint = color;
        bgTint.a = 0.08f;
        Rect bgRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.height);
        EditorGUI.DrawRect(bgRect, bgTint);

        // Draw tag label on the right side
        if (!string.IsNullOrEmpty(tag))
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleRight;
            style.fontSize = 9;

            Rect labelRect = new Rect(selectionRect.xMax - 80, selectionRect.y, 78, selectionRect.height);
            GUI.Label(labelRect, tag, style);
        }
    }

    private static Color GetCategoryColor(GameObject go, out string tag)
    {
        string name = go.name;

        // ── Managers (green) ──
        if (name.Contains("Manager") || name == "GameConstants" || name == "FarmGrid" || name == "SaveManager")
        {
            tag = "MGR";
            return new Color(0.3f, 0.85f, 0.4f);
        }

        // ── Cameras & Lights (yellow) ──
        if (go.GetComponent<Camera>() != null || name.Contains("Camera"))
        {
            tag = "CAM";
            return new Color(1f, 0.9f, 0.3f);
        }
        if (name.Contains("Light"))
        {
            tag = "LIGHT";
            return new Color(1f, 0.9f, 0.3f);
        }

        // ── UI Canvases (blue) ──
        if (go.GetComponent<Canvas>() != null)
        {
            tag = "UI";
            return new Color(0.4f, 0.7f, 1f);
        }

        // ── UI Panels & Popups (lighter blue) ──
        if (name.Contains("Panel") || name.Contains("Popup") || name.Contains("Drawer") || name.Contains("BottomNav"))
        {
            tag = "PANEL";
            return new Color(0.5f, 0.75f, 0.95f);
        }

        // ── Buttons (cyan) ──
        if (name.Contains("Button") && go.GetComponent<UnityEngine.UI.Button>() != null)
        {
            tag = "BTN";
            return new Color(0.4f, 0.85f, 0.85f);
        }

        // ── Equipment & Fences (orange) ──
        if (name.Contains("Fence") || name.Contains("Scarecrow") || name.Contains("Sprinkler") || name.Contains("Equipment"))
        {
            tag = "EQUIP";
            return new Color(1f, 0.65f, 0.25f);
        }

        // ── Environment & Decorations (earthy brown) ──
        if (name == "Environment" || name.Contains("Decoration") || name.Contains("Tree") ||
            name.Contains("Rock") || name.Contains("Log") || name.Contains("Boulder") || name.Contains("Post_"))
        {
            tag = "ENV";
            return new Color(0.75f, 0.6f, 0.35f);
        }

        // ── Farm entities (teal) ──
        if (name.Contains("FarmDog") || name.Contains("Helper") || name.Contains("Zone"))
        {
            tag = "FARM";
            return new Color(0.35f, 0.8f, 0.7f);
        }

        // ── Grid & Tiles (muted green) ──
        if (name == "Grid" || name.Contains("Tilemap") || name.StartsWith("Tile_"))
        {
            tag = "GRID";
            return new Color(0.5f, 0.7f, 0.45f);
        }

        // ── Debug/Test objects (grey) ──
        if (name.Contains("DEBUG") || name.Contains("Test") || name.Contains("PlantTest"))
        {
            tag = "DEBUG";
            return new Color(0.6f, 0.6f, 0.6f);
        }

        // ── Event System (dim) ──
        if (name == "EventSystem")
        {
            tag = "SYS";
            return new Color(0.55f, 0.55f, 0.55f);
        }

        tag = null;
        return Color.clear;
    }
}
