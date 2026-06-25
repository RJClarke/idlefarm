using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Seed-bag widgets stacked up the RIGHT side of the screen, one per active crop (up to 4).
/// Each shows the crop's seed-packet icon and the seeds remaining in its current bag, turning
/// red at 0. When a bag is auto-bought, the widget pops and a "-$cost" floats down from it —
/// anchoring the idea that the bots had to buy a bag to keep planting.
/// Code-built (no UXML) for a trial-quality HUD.
/// </summary>
public class SeedCounterHUD : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;

    // Layout (screen pixels; canvas is ConstantPixelSize like FloatingTextManager).
    private const float WidgetW = 110f;
    private const float WidgetH = 134f;
    private const float Spacing = 14f;
    private const float RightMargin = 16f;
    private const float BottomStart = 300f; // clear of the bottom nav

    private Canvas _canvas;

    private class Bag
    {
        public RectTransform root;
        public TextMeshProUGUI count;
        public TextMeshProUGUI price;
    }

    private float _priceTimer;

    private readonly Dictionary<CropData, Bag> _bags = new Dictionary<CropData, Bag>();

    private void Awake()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 400;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();
    }

    private bool _subscribed;

    // Subscribe in BOTH OnEnable and Start: OnEnable can run before SeedInventory.Awake sets
    // its Instance (scene-load race), but Start always runs after every Awake, so it catches it.
    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || SeedInventory.Instance == null || RunManager.Instance == null) return;
        SeedInventory.Instance.OnSeedCountChanged += HandleCountChanged;
        SeedInventory.Instance.OnBagPurchased += HandleBagPurchased;
        RunManager.Instance.OnRunStarted += PopulateConfiguredCrops;
        RunManager.Instance.OnRunEnded += Clear;
        _subscribed = true;

        // Catch-up: resume-on-reopen fires OnRunStarted during load, possibly before we
        // subscribed — so if a run is already active, build the widgets now.
        if (RunManager.Instance.IsRunActive) PopulateConfiguredCrops();
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (SeedInventory.Instance != null)
        {
            SeedInventory.Instance.OnSeedCountChanged -= HandleCountChanged;
            SeedInventory.Instance.OnBagPurchased -= HandleBagPurchased;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= PopulateConfiguredCrops;
            RunManager.Instance.OnRunEnded -= Clear;
        }
        _subscribed = false;
    }

    private void Clear()
    {
        foreach (var b in _bags.Values) if (b.root != null) Destroy(b.root.gameObject);
        _bags.Clear();
    }

    private static readonly Color RedColor = new Color(0.95f, 0.25f, 0.25f);
    private static readonly Color PriceGold = new Color(0.47f, 0.902f, 0.51f); // match cash/money green

    /// <summary>
    /// Build a persistent widget for every crop configured this run, so the player always sees
    /// each crop's seed count + (escalating) cost for the whole run — including crops the helpers
    /// currently can't afford (those show 0 seeds in red and a red price).
    /// </summary>
    private void PopulateConfiguredCrops()
    {
        Clear();
        if (HelperManager.Instance == null || SeedInventory.Instance == null) return;
        foreach (var crop in HelperManager.Instance.GetConfiguredCrops())
        {
            Bag bag = GetOrCreateBag(crop);
            int remaining = SeedInventory.Instance.SeedsRemaining(crop);
            bag.count.text = remaining.ToString();
            bag.count.color = remaining <= 0 ? RedColor : Color.white;
        }
        RefreshPrices();
    }

    // Bag price escalates with run time, so refresh the price labels on a throttle.
    private void Update()
    {
        if (SeedInventory.Instance == null || _bags.Count == 0) return;
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) return;

        _priceTimer += Time.unscaledDeltaTime;
        if (_priceTimer < 0.75f) return;
        _priceTimer = 0f;

        RefreshPrices();
    }

    /// <summary>Refresh each bag's price text + color: gold if the player can afford a bag, red if not.</summary>
    private void RefreshPrices()
    {
        if (SeedInventory.Instance == null) return;
        foreach (var kv in _bags)
        {
            if (kv.Value.price == null) continue;
            int cost = SeedInventory.Instance.BagCost(kv.Key);
            kv.Value.price.text = "$" + cost;
            bool canBuy = CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordMoney(cost);
            kv.Value.price.color = canBuy ? PriceGold : RedColor;
        }
    }

    private void HandleCountChanged(CropData crop, int remaining)
    {
        if (crop == null) return;
        // Only refresh cards that belong to THIS run's configured crops. Never create one
        // here — otherwise a stray seed-count event for a crop outside the current config
        // (stale SeedInventory state) spawns a card that lingers until the next run.
        if (!_bags.TryGetValue(crop, out var bag) || bag.root == null) return;
        bag.count.text = remaining.ToString();
        bag.count.color = remaining <= 0 ? new Color(0.95f, 0.25f, 0.25f) : Color.white;
    }

    private void HandleBagPurchased(CropData crop, int cost)
    {
        if (crop == null) return;
        if (!_bags.TryGetValue(crop, out var bag) || bag.root == null) return;

        // Pop the bag.
        LeanTween.cancel(bag.root.gameObject);
        bag.root.localScale = Vector3.one;
        LeanTween.scale(bag.root.gameObject, Vector3.one * 1.25f, 0.12f)
            .setEaseOutQuad().setIgnoreTimeScale(true)
            .setOnComplete(() =>
            {
                if (bag.root != null)
                    LeanTween.scale(bag.root.gameObject, Vector3.one, 0.12f).setIgnoreTimeScale(true);
            });

        // Float the "-$cost" down from the bag.
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, bag.root.position);
        screenPos += new Vector2(-WidgetW * 0.5f, -10f);
        FloatingTextManager.ShowMoneySpentAtScreen(cost, screenPos);
    }

    private Bag GetOrCreateBag(CropData crop)
    {
        if (_bags.TryGetValue(crop, out var existing) && existing.root != null)
            return existing;

        int index = _bags.Count;

        var rootGo = new GameObject($"SeedBag_{crop.cropName}", typeof(RectTransform));
        rootGo.transform.SetParent(_canvas.transform, false);
        var root = rootGo.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = new Vector2(WidgetW, WidgetH);
        root.anchoredPosition = new Vector2(-RightMargin, BottomStart + index * (WidgetH + Spacing));

        // Background panel for legibility behind the icon.
        var bgGo = new GameObject("bg", typeof(RectTransform));
        bgGo.transform.SetParent(root, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        bgImg.raycastTarget = false;
        Stretch(bgGo.GetComponent<RectTransform>());

        // Seed-packet icon (fallback to crop sprite). Sits in the upper portion of the widget.
        Sprite icon = crop.seedPacketSprite != null ? crop.seedPacketSprite : crop.cropSprite;
        if (icon != null)
        {
            var iconGo = new GameObject("icon", typeof(RectTransform));
            iconGo.transform.SetParent(root, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.sizeDelta = new Vector2(WidgetW - 24f, WidgetH - 40f);
            irt.anchoredPosition = new Vector2(0f, -8f);
        }

        // Seed count label (seeds remaining).
        var countGo = new GameObject("count", typeof(RectTransform));
        countGo.transform.SetParent(root, false);
        var count = countGo.AddComponent<TextMeshProUGUI>();
        count.fontSize = 30;
        count.fontStyle = FontStyles.Bold;
        count.alignment = TextAlignmentOptions.Center;
        count.raycastTarget = false;
        if (font != null) count.font = font;
        var crt = countGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(0f, 34f);
        crt.anchoredPosition = new Vector2(0f, 30f);

        // Live (escalating) bag price across the very bottom.
        var priceGo = new GameObject("price", typeof(RectTransform));
        priceGo.transform.SetParent(root, false);
        var price = priceGo.AddComponent<TextMeshProUGUI>();
        price.fontSize = 24;
        price.fontStyle = FontStyles.Bold;
        price.alignment = TextAlignmentOptions.Center;
        price.raycastTarget = false;
        price.color = new Color(1f, 0.85f, 0.4f);
        if (font != null) price.font = font;
        var prt = priceGo.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(0f, 30f);
        prt.anchoredPosition = new Vector2(0f, 4f);

        var bag = new Bag { root = root, count = count, price = price };
        _bags[crop] = bag;
        return bag;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
