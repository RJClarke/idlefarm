using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1100)]
public class CompostBoostModalUITK : MonoBehaviour
{
    public static CompostBoostModalUITK Instance { get; private set; }

    /// <summary>Boost token pricing: (multiplier, durationSecs, compostCost).</summary>
    private static readonly (float multiplier, float durationSecs, int cost)[] Tokens = new[]
    {
        (2f,  4f * 3600f,   50),
        (3f,  4f * 3600f,  150),
        (4f,  4f * 3600f,  400),
        (2f, 12f * 3600f,  120),
        (3f, 12f * 3600f,  360),
        (4f, 12f * 3600f, 1000),
    };

    private UIDocument document;
    private VisualElement root;
    private VisualElement modalRoot;
    private VisualElement boostList;
    private Button closeButton;
    private VisualElement backdrop;

    private int targetSlotIndex = -1;
    private bool isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable() => CacheAndWire();
    private void Start()    { if (root == null) CacheAndWire(); }

    private void CacheAndWire()
    {
        root = document.rootVisualElement;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;

        modalRoot   = root.Q<VisualElement>("modal-root");
        boostList   = root.Q<VisualElement>("boost-list");
        closeButton = root.Q<Button>("modal-close");
        backdrop    = root.Q<VisualElement>("modal-backdrop");

        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (backdrop != null)    backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open(int slotIndex)
    {
        if (root == null) CacheAndWire();
        targetSlotIndex = slotIndex;
        isOpen = true;
        if (root != null) root.pickingMode = PickingMode.Position;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.Flex;
        Rebuild();
    }

    public void Close()
    {
        isOpen = false;
        targetSlotIndex = -1;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void Rebuild()
    {
        if (boostList == null) return;
        boostList.Clear();

        int compostBalance = CurrencyManager.Instance != null ? CurrencyManager.Instance.Compost : 0;

        foreach (var token in Tokens)
        {
            var row = new VisualElement(); row.AddToClassList("boost-row");
            string hrs = (token.durationSecs / 3600f).ToString("F0");
            var label = new Label($"{token.multiplier:F0}× for {hrs} hr"); label.AddToClassList("boost-row__label");
            var cost  = new Label($"{token.cost} 🌱"); cost.AddToClassList("boost-row__cost");
            row.Add(label); row.Add(cost);

            bool affordable = compostBalance >= token.cost;
            if (!affordable)
                row.AddToClassList("boost-row--disabled");
            else
            {
                var captured = token;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (ResearchManager.Instance != null &&
                        ResearchManager.Instance.TryApplyBoost(targetSlotIndex, captured.multiplier, captured.durationSecs, captured.cost))
                    {
                        Close();
                    }
                });
            }
            boostList.Add(row);
        }
    }
}
