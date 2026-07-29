using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

/// <summary>TEMPORARY diagnostic — probes which UITK panel picks at screen centre (where no popup
/// should be), to find a full-screen pickable element that is blocking world taps. Delete after use.</summary>
public static class _ClickDebugProbe
{
    [MenuItem("Tools/Debug/Teleport Frame Woods")]
    private static void FrameWoods() => Teleport(new Vector3(20f, -30f, -10f));

    [MenuItem("Tools/Debug/Teleport Frame Cannery")]
    private static void FrameCannery() => Teleport(new Vector3(10f, -8f, -10f));

    private static void Teleport(Vector3 pos)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Probe] Enter Play mode first."); return; }
        var cam = Camera.main;
        if (cam == null) return;
        // Disable the touch controller so it can't snap us back, then move.
        foreach (var mb in cam.GetComponents<MonoBehaviour>())
            if (mb.GetType().Name == "MobileCameraController") mb.enabled = false;
        cam.transform.position = pos;
        Debug.Log($"[Probe] Teleported camera to {pos} (MobileCameraController disabled).");
    }

    [MenuItem("Tools/Debug/Pan To Woods")]
    private static void PanWoods() => Pan(CameraPanController.Location.Woods);

    [MenuItem("Tools/Debug/Pan To Greenhouse")]
    private static void PanGreenhouse() => Pan(CameraPanController.Location.Greenhouse);

    private static void Pan(CameraPanController.Location loc)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Probe] Enter Play mode first."); return; }
        var pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (pan == null) { Debug.LogWarning("[Probe] No CameraPanController on Camera.main."); return; }
        pan.PanTo(loc);
        Debug.Log($"[Probe] PanTo({loc}) requested.");
    }

    [MenuItem("Tools/Debug/Probe UI Picking At Center")]
    private static void Probe()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Probe] Enter Play mode first."); return; }

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Debug.Log($"[Probe] Screen {Screen.width}x{Screen.height}, center {center}");

        // uGUI raycast at centre — this is the EventSystem branch UITapBlocker also consults.
        if (EventSystem.current == null)
        {
            Debug.Log("[Probe] EventSystem.current == NULL");
        }
        else
        {
            var ped = new PointerEventData(EventSystem.current) { position = center };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            var sb = new StringBuilder($"[Probe] uGUI RaycastAll@center hit {hits.Count}: ");
            foreach (var h in hits)
                sb.Append($"[{h.gameObject.name} (module={h.module?.GetType().Name})] ");
            Debug.Log(sb.ToString());
        }

        // Probe UITapBlocker at each world building's actual screen position.
        var cam = Camera.main;
        var pan = cam != null ? cam.GetComponent<CameraPanController>() : null;
        Debug.Log($"[Probe] Camera: pos={(cam!=null?cam.transform.position:default)}, orthoSize={(cam!=null?cam.orthographicSize:0)}, CurrentLocation={(pan!=null?pan.CurrentLocation.ToString():"?")}, IsPanning={(pan!=null?pan.IsPanning.ToString():"?")}, timeScale={Time.timeScale}, LeanTweenActive={LeanTween.tweensRunning}");

        if (pan != null)
        {
            var t = pan.GetType();
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            float panDur = (float)(t.GetField("panDuration", bf)?.GetValue(pan) ?? -1f);
            int tid = (int)(t.GetField("activeTweenId", bf)?.GetValue(pan) ?? -99);
            Debug.Log($"[Probe] panDuration={panDur}, activeTweenId={tid}");
            var locs = t.GetField("locations", bf)?.GetValue(pan) as System.Array;
            if (locs != null)
                foreach (var e in locs)
                {
                    var et = e.GetType();
                    var loc = et.GetField("location").GetValue(e);
                    var off = et.GetField("offset").GetValue(e);
                    var dur = et.GetField("durationOverride").GetValue(e);
                    var ease = et.GetField("easeOverride").GetValue(e);
                    Debug.Log($"[Probe]   LocationOffset {loc}: offset={off}, durationOverride={dur}, ease={ease}");
                }
        }
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            string tn = mb.GetType().Name;
            if (tn != "CanneryBuilding" && tn != "SmokehouseBuilding" && tn != "WoodRack" && tn != "ShopBuilding") continue;
            var sr = mb.GetComponent<SpriteRenderer>();
            var col = mb.GetComponent<Collider2D>();
            Vector3 sp3 = cam != null ? cam.WorldToScreenPoint(mb.transform.position) : Vector3.zero;
            Vector2 sp = sp3;
            bool onScreen = sp3.z > 0 && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height;
            bool blocked = UITapBlocker.PointerOverUI(sp);
            // Reflectively read the private CanInteract() gate.
            var mi = mb.GetType().GetMethod("CanInteract", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object ci = mi != null ? mi.Invoke(mb, null) : "n/a";
            // What collider does an OverlapPoint at the building's own centre return?
            var overlap = Physics2D.OverlapPoint(mb.transform.position);
            bool hitsSelf = overlap == col;
            // uGUI raycast at THIS building's screen position (the real-pointer branch of the guard).
            string uguiHit = "n/a";
            if (onScreen && EventSystem.current != null)
            {
                var ped2 = new PointerEventData(EventSystem.current) { position = sp };
                var hits2 = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped2, hits2);
                var sb2 = new StringBuilder($"{hits2.Count}: ");
                foreach (var h in hits2) sb2.Append($"[{h.gameObject.name}] ");
                uguiHit = sb2.ToString();
            }
            Debug.Log($"[Probe] BUILDING {tn} '{mb.name}': onScreen={onScreen}, screenPos={sp}, CanInteract={ci}, overlapHitsSelf={hitsSelf} (got {(overlap!=null?overlap.name:"null")}), UITapBlocker={blocked}, uGUI@bldg={uguiHit}");
        }

        // Dump every Collider2D with a suspiciously large footprint (the giant-Lake-collider hunt).
        foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            var b = c.bounds;
            if (b.size.x > 20f || b.size.y > 20f)
                Debug.Log($"[Probe] BIG COLLIDER '{c.gameObject.name}' <{c.GetType().Name}>: boundsSize={b.size}, boundsCenter={b.center}, worldPos={c.transform.position}, localScale={c.transform.localScale}, enabled={c.enabled}");
        }

        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        Debug.Log($"[Probe] {docs.Length} UIDocument(s) in scene.");
        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            bool enabled = doc.isActiveAndEnabled;
            if (root == null) { Debug.Log($"[Probe] {doc.name}: enabled={enabled}, root=NULL"); continue; }

            var panel = root.panel;
            string pickedInfo = "n/a";
            if (panel != null)
            {
                Vector2 pp = RuntimePanelUtils.ScreenToPanel(panel, center);
                var picked = panel.Pick(pp);
                pickedInfo = picked == null ? "null"
                    : $"'{picked.name}' <{picked.GetType().Name}> classes=[{string.Join(",", picked.GetClasses())}] display={picked.resolvedStyle.display}";
            }
            Debug.Log($"[Probe] {doc.name}: enabled={enabled}, rootPicking={root.pickingMode}, rootDisplay={root.resolvedStyle.display} → PICK@center: {pickedInfo}");
        }
    }
}
