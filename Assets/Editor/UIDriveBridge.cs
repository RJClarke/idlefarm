using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

/// <summary>
/// File-triggered UI driver for headless/MCP play-testing, companion to PlayModeBridge (which
/// handles enter/exit play + screenshots). Lets an external tool play through UI flows like a
/// fresh-install onboarding without real touches:
///
///   Temp/gameview_size.request  body "1080x2400"   → Game view to that fixed resolution (edit or play)
///   Temp/ui_click.request       body "name"        → click the first UITK element named `name`
///                                                    (any runtime panel) or, failing that, the first
///                                                    active GameObject named `name` (uGUI/world)
///   Temp/ui_pick.request        body "x,y"         → what a tap at that screen point would hit,
///                                                    per UITK panel (top sort order first) + uGUI
///   Temp/ui_tap.request         body "x,y"         → tap whatever is under that screen point
///   Temp/mouse_press.request    body "x,y[,secs]"  → a REAL Input System mouse press (down, held
///                                                    `secs`, default 0.12, then up) — what world-space
///                                                    handlers reading Mouse.current actually see
///   Temp/refresh.request        (empty)            → AssetDatabase.Refresh + script recompile, even
///                                                    while the editor window is unfocused (edit mode)
///   Temp/menu.request           body "Farm Game/Narrative/Seed Missing Copy" → run an editor menu item
///   Temp/asset_string.request   body "assetPath\npropertyPath\nvalue..." → set a string field on
///                                                    an asset via SerializedObject (edit or play)
///
/// Results go to Temp/ui_drive_result.txt. Editor-only; no runtime footprint.
/// </summary>
[InitializeOnLoad]
public static class UIDriveBridge
{
    private const string SizeRequest = "Temp/gameview_size.request";
    private const string ClickRequest = "Temp/ui_click.request";
    private const string PickRequest = "Temp/ui_pick.request";
    private const string TapRequest = "Temp/ui_tap.request";
    private const string MouseRequest = "Temp/mouse_press.request";
    private const string AssetStringRequest = "Temp/asset_string.request";
    private const string RefreshRequest = "Temp/refresh.request";
    private const string PressRequest = "Temp/ui_press.request";
    private const string MenuRequest = "Temp/menu.request";
    private const string ScrollRequest = "Temp/ui_scroll.request";
    private const string ResultPath = "Temp/ui_drive_result.txt";

    private static double nextPoll;

    static UIDriveBridge() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        TickMouseRelease(); // every editor tick, not the 1s poll, so short holds stay short
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;

        // Refresh/recompile from the file bridge: EditorApplication.update keeps ticking while the
        // editor is unfocused (e.g. behind a fullscreen game), where auto-refresh and the MCP
        // compile call don't fire. Works in edit mode only — Unity won't recompile mid-play.
        if (File.Exists(RefreshRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(RefreshRequest);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            File.WriteAllText(ResultPath, "OK: refresh + compile requested");
            return;
        }

        string result = null;
        try
        {
            if (File.Exists(SizeRequest)) result = SetGameViewSize(Consume(SizeRequest));
            else if (File.Exists(ClickRequest)) result = Click(Consume(ClickRequest));
            else if (File.Exists(PickRequest)) result = Pick(Consume(PickRequest));
            else if (File.Exists(TapRequest)) result = Tap(Consume(TapRequest));
            else if (File.Exists(PressRequest)) result = Press(Consume(PressRequest));
            else if (File.Exists(MouseRequest)) result = MousePress(Consume(MouseRequest));
            else if (File.Exists(ScrollRequest)) result = Scroll(Consume(ScrollRequest));
            else if (File.Exists(AssetStringRequest)) result = SetAssetString(Consume(AssetStringRequest, trim: false));
            else if (File.Exists(MenuRequest))
            {
                string item = Consume(MenuRequest);
                result = EditorApplication.ExecuteMenuItem(item) ? "OK: ran menu " + item : "MENU NOT FOUND: " + item;
            }
        }
        catch (System.Exception e) { result = "ERROR: " + e; }
        if (result != null) File.WriteAllText(ResultPath, result);
    }

    // ── Real mouse press (Input System) ────────────────────────────

    private static double mouseReleaseAt = -1;
    private static Vector2 mousePos;

    private static string MousePress(string body)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";
        if (UnityEngine.InputSystem.Mouse.current == null) return "NO MOUSE";
        string[] p = body.Split(',');
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        mousePos = new Vector2(float.Parse(p[0], inv), float.Parse(p[1], inv)); // bottom-left origin px
        double hold = p.Length > 2 ? double.Parse(p[2], inv) : 0.12;
        QueueMouse(mousePos, false); // move there first so the press lands at the point
        QueueMouse(mousePos, true);
        mouseReleaseAt = EditorApplication.timeSinceStartup + hold;
        return $"OK: mouse down at {mousePos}, release in {hold:0.##}s";
    }

    private static void TickMouseRelease()
    {
        if (mouseReleaseAt < 0 || EditorApplication.timeSinceStartup < mouseReleaseAt) return;
        mouseReleaseAt = -1;
        if (EditorApplication.isPlaying && UnityEngine.InputSystem.Mouse.current != null) QueueMouse(mousePos, false);
    }

    private static void QueueMouse(Vector2 pos, bool down) =>
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,
            new UnityEngine.InputSystem.LowLevel.MouseState { position = pos, buttons = (ushort)(down ? 1 : 0) });

    private static string Consume(string path, bool trim = true)
    {
        string body = File.ReadAllText(path);
        File.Delete(path);
        return trim ? body.Trim() : body;
    }

    /// <summary>Body: line 1 = asset path, line 2 = serialized property path, rest = the new string
    /// value (newlines kept). Edits through SerializedObject + SaveAssets rather than hand-editing
    /// the .asset YAML, so Unity's in-memory copy and the file never disagree.</summary>
    private static string SetAssetString(string body)
    {
        body = body.Replace("\r\n", "\n");
        string[] parts = body.Split(new[] { '\n' }, 3);
        if (parts.Length < 3) return "ERROR: need asset path, property path, value";
        Object asset = AssetDatabase.LoadMainAssetAtPath(parts[0].Trim());
        if (asset == null) return "ASSET NOT FOUND: " + parts[0];
        var so = new SerializedObject(asset);
        SerializedProperty prop = so.FindProperty(parts[1].Trim());
        if (prop == null || prop.propertyType != SerializedPropertyType.String) return "STRING PROPERTY NOT FOUND: " + parts[1];
        prop.stringValue = parts[2].TrimEnd('\n');
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return $"OK: {parts[0].Trim()} {parts[1].Trim()} = {prop.stringValue.Length} chars";
    }

    // ── Game view size ─────────────────────────────────────────────

    private static string SetGameViewSize(string body)
    {
        string[] parts = body.ToLowerInvariant().Split('x');
        int w = int.Parse(parts[0]), h = int.Parse(parts[1]);

        Assembly ed = typeof(Editor).Assembly;
        System.Type sizesType = ed.GetType("UnityEditor.GameViewSizes");
        System.Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance").GetValue(null);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)CurrentGroupType(sizesType, sizes) });
        System.Type groupType = group.GetType();

        // Reuse an existing fixed size with these dimensions, else add one.
        int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            object s = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            System.Type st = s.GetType();
            if ((int)st.GetProperty("width").GetValue(s) == w && (int)st.GetProperty("height").GetValue(s) == h
                && st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution")
            { index = i; break; }
        }
        if (index < 0)
        {
            System.Type sizeType = ed.GetType("UnityEditor.GameViewSize");
            System.Type sizeKind = ed.GetType("UnityEditor.GameViewSizeType");
            object fixedKind = System.Enum.Parse(sizeKind, "FixedResolution");
            object newSize = System.Activator.CreateInstance(sizeType, fixedKind, w, h, $"Phone {w}x{h}");
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
            index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }

        System.Type gvType = ed.GetType("UnityEditor.GameView");
        Object[] views = Resources.FindObjectsOfTypeAll(gvType);
        if (views.Length == 0) return "NO GAME VIEW OPEN";
        foreach (Object gv in views)
        {
            MethodInfo cb = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (cb != null) cb.Invoke(gv, new object[] { index, null });
            else gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(gv, index);
            ((EditorWindow)gv).Repaint();
        }
        return $"OK: game view {w}x{h} (index {index})";
    }

    private static GameViewSizeGroupType CurrentGroupType(System.Type sizesType, object sizes)
    {
        PropertyInfo p = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return p != null ? (GameViewSizeGroupType)p.GetValue(sizes) : GameViewSizeGroupType.Standalone;
    }

    // ── Scroll ─────────────────────────────────────────────────────

    private static string Scroll(string body)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";
        string[] parts = body.Split(',');
        if (parts.Length < 3) return "ERROR: need name,x,y";
        // Several popups reuse names like "section-list"; scroll the one that can actually scroll
        // (the open popup), not just the first match.
        ScrollView best = null;
        Vector2 max = Vector2.zero;
        foreach (UIDocument doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (!(doc.rootVisualElement?.Q<VisualElement>(parts[0].Trim()) is ScrollView sv)) continue;
            Vector2 m = new Vector2(
                Mathf.Max(0f, sv.contentContainer.layout.width - sv.contentViewport.layout.width),
                Mathf.Max(0f, sv.contentContainer.layout.height - sv.contentViewport.layout.height));
            if (best == null || m.sqrMagnitude > max.sqrMagnitude) { best = sv; max = m; }
        }
        if (best == null) return "SCROLLVIEW NOT FOUND: " + parts[0];
        float Axis(string v, float end) => v.Trim() == "end" ? end : float.Parse(v.Trim());
        best.scrollOffset = new Vector2(Axis(parts[1], max.x), Axis(parts[2], max.y));
        return $"OK: '{parts[0].Trim()}' offset {best.scrollOffset} (max {max})";
    }

    // ── Click ──────────────────────────────────────────────────────

    private static string Click(string name)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";

        foreach (UIDocument doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            VisualElement el = doc.rootVisualElement?.Q<VisualElement>(name);
            if (el == null) continue;
            if (el.resolvedStyle.display == DisplayStyle.None) continue;
            // Popups register ClickEvent callbacks; Button.clicked hangs off its Clickable.
            using (ClickEvent ev = ClickEvent.GetPooled())
            {
                ev.target = el;
                el.SendEvent(ev);
            }
            if (el is Button b && b.clickable != null)
            {
                FieldInfo clickedField = typeof(Clickable).GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic);
                (clickedField?.GetValue(b.clickable) as System.Action)?.Invoke();
            }
            return $"OK: UITK '{name}' in {doc.gameObject.name}";
        }

        GameObject go = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(t => t.gameObject).FirstOrDefault(g => g.name == name);
        if (go == null) return "NOT FOUND: " + name;
        var data = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        go.SendMessage("OnMouseDown", SendMessageOptions.DontRequireReceiver);
        go.SendMessage("OnMouseUpAsButton", SendMessageOptions.DontRequireReceiver);
        return $"OK: GameObject '{name}'";
    }

    // ── Pick (what would a tap at x,y hit?) ────────────────────────

    private static string Pick(string body)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";
        string[] p = body.Split(',');
        Vector2 screen = new Vector2(float.Parse(p[0]), float.Parse(p[1])); // bottom-left origin px
        var sb = new StringBuilder($"PICK at screen {screen} (screen {Screen.width}x{Screen.height})\n");

        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Where(d => d.rootVisualElement?.panel != null && d.panelSettings != null)
            .OrderByDescending(d => d.panelSettings.sortingOrder);
        var seen = new HashSet<IPanel>();
        foreach (UIDocument doc in docs)
        {
            IPanel panel = doc.rootVisualElement.panel;
            if (!seen.Add(panel)) continue;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement hit = panel.Pick(panelPos);
            sb.AppendLine($"  UITK sort {doc.panelSettings.sortingOrder} [{doc.gameObject.name}] panel{panelPos} -> {Describe(hit)}");
        }

        if (EventSystem.current != null)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, results);
            foreach (RaycastResult r in results.Take(5))
                sb.AppendLine($"  uGUI -> {Path(r.gameObject.transform)} (sort {r.sortingOrder})");
        }
        return sb.ToString();
    }

    /// <summary>Tap at a screen point (bottom-left origin): clicks the topmost UITK element under it
    /// (highest panel sort order first, ClickEvent bubbles to row/card handlers), else the top uGUI
    /// raycast hit — the same resolution a real finger gets.</summary>
    private static string Tap(string body)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";
        string[] p = body.Split(',');
        Vector2 screen = new Vector2(float.Parse(p[0]), float.Parse(p[1]));

        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Where(d => d.rootVisualElement?.panel != null && d.panelSettings != null)
            .OrderByDescending(d => d.panelSettings.sortingOrder);
        foreach (UIDocument doc in docs)
        {
            IPanel panel = doc.rootVisualElement.panel;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement hit = panel.Pick(panelPos);
            if (hit == null) continue;
            using (ClickEvent ev = ClickEvent.GetPooled())
            {
                ev.target = hit;
                hit.SendEvent(ev);
            }
            for (VisualElement e = hit; e != null; e = e.parent)
                if (e is Button b && b.clickable != null)
                {
                    FieldInfo clickedField = typeof(Clickable).GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic);
                    (clickedField?.GetValue(b.clickable) as System.Action)?.Invoke();
                    break;
                }
            return $"OK: tapped UITK {Describe(hit)}";
        }

        if (EventSystem.current != null)
        {
            var results = new List<RaycastResult>();
            var data = new PointerEventData(EventSystem.current) { position = screen };
            EventSystem.current.RaycastAll(data, results);
            RaycastResult top = results.FirstOrDefault(r => r.gameObject != null && r.gameObject.GetComponentInParent<UIDocument>() == null
                                                            && r.gameObject.name != "RunewoodPanelSettings");
            if (top.gameObject != null)
            {
                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject);
                if (handler != null) ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
                return $"OK: tapped uGUI {Path(top.gameObject.transform)} (handler {(handler != null ? handler.name : "none")})";
            }
        }
        return "NOTHING UNDER " + screen;
    }

    /// <summary>
    /// Closest thing to a real finger for UI Toolkit: a pointer DOWN then UP at a screen point, sent
    /// to the top panel that has something there, which lets UITK pick the target, run Button
    /// Clickables and synthesize the ClickEvent itself (ui_tap hands a ready-made ClickEvent to one
    /// element, which some handlers never see). Falls back to ui_tap's uGUI path when no panel hits.
    /// </summary>
    private static string Press(string body)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";
        string[] p = body.Split(',');
        Vector2 screen = new Vector2(float.Parse(p[0]), float.Parse(p[1]));

        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Where(d => d.rootVisualElement?.panel != null && d.panelSettings != null)
            .OrderByDescending(d => d.panelSettings.sortingOrder);
        foreach (UIDocument doc in docs)
        {
            IPanel panel = doc.rootVisualElement.panel;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement hit = panel.Pick(panelPos);
            if (hit == null) continue;

            VisualElement root = panel.visualTree;
            var down = new Event { type = EventType.MouseDown, mousePosition = panelPos, button = 0, clickCount = 1 };
            using (PointerDownEvent e = PointerDownEvent.GetPooled(down)) root.SendEvent(e);
            var up = new Event { type = EventType.MouseUp, mousePosition = panelPos, button = 0, clickCount = 1 };
            using (PointerUpEvent e = PointerUpEvent.GetPooled(up)) root.SendEvent(e);
            return $"OK: pressed UITK {Describe(hit)}";
        }
        return Tap(body);
    }

    private static string Describe(VisualElement el)
    {
        if (el == null) return "(nothing)";
        var chain = new List<string>();
        for (VisualElement e = el; e != null && chain.Count < 4; e = e.parent)
            chain.Add(string.IsNullOrEmpty(e.name) ? e.GetType().Name : e.name);
        return string.Join(" < ", chain);
    }

    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
