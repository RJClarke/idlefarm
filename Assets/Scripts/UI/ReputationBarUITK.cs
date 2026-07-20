using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Always-on horizontal rep bar pinned to the top of the Market view (Reputation Phase 2, spec
/// §3.4). Fully code-driven UIDocument (ToastManager pattern) — no UXML/USS assets, no scene
/// wiring beyond placing this component. Tapping it opens the Town Requests popup.
/// </summary>
[DefaultExecutionOrder(1050)]
public class ReputationBarUITK : MonoBehaviour
{
    public static ReputationBarUITK Instance { get; private set; }
    private const int SORT_ORDER = 900;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement barRoot;
    private VisualElement fill;
    private Label progressLabel;
    private Label pointsLabel;

    private CameraPanController panController;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        document = GetComponent<UIDocument>();
        if (document == null) document = gameObject.AddComponent<UIDocument>();

        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.name = "ReputationBarPanelSettings (runtime)";
        runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        runtimePanelSettings.referenceResolution = new Vector2Int(1080, 1920);
        runtimePanelSettings.match = 0.5f;
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        Build();
        RefreshVisibility();
        Refresh();

        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged += Refresh;

        CameraPanController pan = GetPanController();
        if (pan != null)
        {
            pan.OnPanStarted += OnLocationChanging;
            pan.OnPanCompleted += OnLocationChanging;
        }
    }

    private void OnDestroy()
    {
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged -= Refresh;
        if (panController != null)
        {
            panController.OnPanStarted -= OnLocationChanging;
            panController.OnPanCompleted -= OnLocationChanging;
        }
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private CameraPanController GetPanController()
    {
        if (panController == null && Camera.main != null)
            panController = Camera.main.GetComponent<CameraPanController>();
        return panController;
    }

    private void OnLocationChanging(CameraPanController.Location _) => RefreshVisibility();

    private void RefreshVisibility()
    {
        if (barRoot == null) return;
        CameraPanController pan = GetPanController();
        bool atMarket = pan != null && pan.CurrentLocation == CameraPanController.Location.Market;
        barRoot.style.display = atMarket ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Build()
    {
        VisualElement root = document.rootVisualElement;
        if (root == null) { Debug.LogWarning("[ReputationBarUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;

        barRoot = new VisualElement { name = "rep-bar-root" };
        barRoot.style.position = Position.Absolute;
        barRoot.style.top = 20;
        barRoot.style.left = Length.Percent(10);
        barRoot.style.right = Length.Percent(10);
        barRoot.style.height = 64;
        barRoot.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.9f);
        barRoot.style.borderTopLeftRadius = 16; barRoot.style.borderTopRightRadius = 16;
        barRoot.style.borderBottomLeftRadius = 16; barRoot.style.borderBottomRightRadius = 16;
        barRoot.style.paddingLeft = 6; barRoot.style.paddingRight = 14;
        barRoot.style.flexDirection = FlexDirection.Row;
        barRoot.style.alignItems = Align.Center;
        barRoot.pickingMode = PickingMode.Position;
        barRoot.RegisterCallback<ClickEvent>(_ => TownRequestsPopupUITK.Instance?.Open());
        root.Add(barRoot);

        VisualElement track = new VisualElement { name = "rep-bar-track" };
        track.style.flexGrow = 1;
        track.style.height = 24;
        track.style.marginLeft = 10; track.style.marginRight = 10;
        track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
        track.style.borderTopLeftRadius = 12; track.style.borderTopRightRadius = 12;
        track.style.borderBottomLeftRadius = 12; track.style.borderBottomRightRadius = 12;
        track.style.overflow = Overflow.Hidden;
        barRoot.Add(track);

        fill = new VisualElement { name = "rep-bar-fill" };
        fill.style.height = Length.Percent(100);
        fill.style.width = Length.Percent(0);
        fill.style.backgroundColor = new Color(0.78f, 0.35f, 0.85f); // purple: reputation
        track.Add(fill);

        progressLabel = new Label();
        progressLabel.style.position = Position.Absolute;
        progressLabel.style.left = 0; progressLabel.style.right = 0; progressLabel.style.top = 0; progressLabel.style.bottom = 0;
        progressLabel.style.color = Color.white;
        progressLabel.style.fontSize = 18;
        progressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        track.Add(progressLabel);

        pointsLabel = new Label();
        pointsLabel.style.color = new Color(1f, 0.84f, 0f);
        pointsLabel.style.fontSize = 20;
        pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pointsLabel.style.display = DisplayStyle.None;
        barRoot.Add(pointsLabel);
    }

    private void Refresh()
    {
        if (fill == null || ReputationManager.Instance == null) return;
        var rm = ReputationManager.Instance;
        fill.style.width = Length.Percent(rm.BarProgress01 * 100f);
        progressLabel.text = $"Reputation  {rm.BarProgress} / {rm.NextPointCost}";
        if (rm.UnspentPoints > 0)
        {
            pointsLabel.text = $"Points: {rm.UnspentPoints}";
            pointsLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            pointsLabel.style.display = DisplayStyle.None;
        }
    }
}
