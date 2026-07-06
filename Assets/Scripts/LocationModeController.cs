using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central visibility switchboard for the camera's current Location.
/// Subscribes to <see cref="CameraPanController.OnPanStarted"/> so things hide at the START
/// of a trip to Market (not after the camera arrives) and unhide at the START of a trip back.
///
/// Drop GameObjects into the inspector lists. No per-object scripts needed.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LocationModeController : MonoBehaviour
{
    [Header("Hidden while at Market (HUD, nav, etc.)")]
    [Tooltip("Toggled via SetActive. Use for UI that should disappear entirely at Market.")]
    [SerializeField] private GameObject[] hideAtMarket;

    [Tooltip("Same as hideAtMarket but resolved by scene-name lookup at Start. Useful when inspector " +
             "references can't easily be wired (e.g. MCP automation). Names are resolved via " +
             "GameObject.Find — searches are root-relative scene paths or bare names.")]
    [SerializeField] private string[] hideAtMarketByName;

    [Header("Hidden while at Market — entity managers")]
    [Tooltip("Renderers + Colliders disabled (manager keeps ticking) so animals/helpers don't wander into view.")]
    [SerializeField] private GameObject[] hideEntitiesAtMarket;

    private CameraPanController panController;
    private readonly List<GameObject> resolvedHideAtMarket = new List<GameObject>();

    private void Start()
    {
        ResolveByName();

        panController = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (panController == null)
        {
            Debug.LogWarning("[LocationModeController] No CameraPanController found on Main Camera.");
            return;
        }
        panController.OnPanStarted   += OnPanStarted;
        panController.OnPanCompleted += OnPanCompleted;
        Apply(panController.CurrentLocation);
    }

    private void ResolveByName()
    {
        resolvedHideAtMarket.Clear();
        if (hideAtMarket != null)
            for (int i = 0; i < hideAtMarket.Length; i++)
                if (hideAtMarket[i] != null) resolvedHideAtMarket.Add(hideAtMarket[i]);

        if (hideAtMarketByName == null) return;
        for (int i = 0; i < hideAtMarketByName.Length; i++)
        {
            string n = hideAtMarketByName[i];
            if (string.IsNullOrEmpty(n)) continue;
            GameObject go = GameObject.Find(n);
            if (go != null) resolvedHideAtMarket.Add(go);
            else Debug.LogWarning($"[LocationModeController] hideAtMarketByName: no GameObject found for '{n}'.");
        }
    }

    private void OnDestroy()
    {
        if (panController != null)
        {
            panController.OnPanStarted   -= OnPanStarted;
            panController.OnPanCompleted -= OnPanCompleted;
        }
    }

    private void OnPanStarted(CameraPanController.Location target) => Apply(target);
    private void OnPanCompleted(CameraPanController.Location loc) => Apply(loc);

    private void Apply(CameraPanController.Location loc)
    {
        bool atMarket = loc == CameraPanController.Location.Market;

        for (int i = 0; i < resolvedHideAtMarket.Count; i++)
        {
            GameObject go = resolvedHideAtMarket[i];
            if (go != null) go.SetActive(!atMarket);
        }

        for (int i = 0; i < hideEntitiesAtMarket.Length; i++)
        {
            GameObject go = hideEntitiesAtMarket[i];
            if (go == null) continue;
            SetEntityVisible(go, !atMarket);
        }
    }

    private static void SetEntityVisible(GameObject root, bool visible)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        Collider2D[] cols = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = visible;
    }
}
