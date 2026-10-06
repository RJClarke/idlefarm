using UnityEngine;
using UnityEngine.UIElements;

/// <summary>One-time Farmer's Pass pitch after the 3rd ad claim (spec §7): art, title, description,
/// price button, "Maybe later". Self-creating, sort 1450.</summary>
public sealed class PassPitchUITK : MonoBehaviour
{
    private const int SortOrder = 1450;

    private static PassPitchUITK instance;
    private PanelSettings runtimeSettings;
    private VisualElement root, layer;
    private Button buy;

    public static void Show()
    {
        FreeGiftManager.Instance?.MarkPitchShown();
        if (StoreManager.Instance != null && StoreManager.Instance.HasFarmersPass) return;
        if (instance == null) instance = new GameObject("PassPitchUITK").AddComponent<PassPitchUITK>();
        instance.Open();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    private void Open()
    {
        if (root == null)
        {
            root = MonetizationUI.CreateRoot(this, SortOrder, "PassPitch", out runtimeSettings);
            if (root == null) return;
            Build();
        }
        buy.text = StoreManager.Instance != null ? StoreManager.Instance.PriceFor(StoreDefaults.PassId) : "";
        layer.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
    }

    private void Close()
    {
        layer.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
    }

    private void Build()
    {
        layer = new VisualElement { name = "pitch-layer" };
        MonetizationUI.Fill(layer);
        layer.style.backgroundColor = MonetizationUI.Dim(0.75f);
        layer.style.alignItems = Align.Center;
        layer.style.justifyContent = Justify.Center;
        layer.RegisterCallback<ClickEvent>(e => { if (e.target == layer) Close(); });
        root.Add(layer);

        VisualElement card = MonetizationUI.Card();
        card.style.width = Length.Percent(86);
        card.style.maxWidth = 780;
        card.style.alignItems = Align.Center;
        layer.Add(card);

        StoreProductDef pass = StoreCatalogSO.Instance.Get(StoreDefaults.PassId);
        card.Add(MonetizationUI.PassIcon(200, pass != null ? pass.gems : 500)); // no-ads art + "+500" gem badge

        Label title = MonetizationUI.PixelText("Farmer's Pass", 1, MonetizationUI.Ink);
        title.style.marginTop = 8;
        card.Add(title);

        Label desc = MonetizationUI.Text(pass != null ? pass.description : StoreDefaults.PassDescription, 32, MonetizationUI.Walnut, bold: true);
        desc.style.unityTextAlign = TextAnchor.MiddleCenter;
        desc.style.marginTop = 10; desc.style.marginBottom = 20;
        card.Add(desc);

        buy = MonetizationUI.BrownButton("", () => { Close(); StoreManager.Instance?.Purchase(StoreDefaults.PassId); }, 34);
        buy.name = "pitch-buy";
        buy.style.minWidth = 360;
        card.Add(buy);

        Button later = new Button(Close) { text = "Maybe later", name = "pitch-later" };
        later.style.backgroundColor = Color.clear;
        MonetizationUI.NoBorder(later);
        later.style.color = MonetizationUI.Walnut;
        later.style.fontSize = 26;
        later.style.marginTop = 12;
        card.Add(later);
    }
}
