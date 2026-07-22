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
    [Tooltip("9-sliced frame drawn behind each seed bag (the wooden slot look). Falls back to a " +
             "flat dark panel if unset.")]
    [SerializeField] private Sprite frameSprite;

    // Layout is authored in 1080x1920 reference pixels (the canvas below scales with screen size,
    // matching the rest of the UI, so these line up with the map-nav buttons on every resolution).
    private const float WidgetW = 110f;
    private const float WidgetH = 150f;   // a touch taller so the icon + price both breathe
    private const float Spacing = 14f;
    private const float RightMargin = 16f;
    private const float BadgeSize = 46f;   // cream/brown seeds-remaining badge in the top-right corner
    // Sit the stack above the map-nav button column (Greenhouse/Lake/Woods top out ~436) so the two
    // never overlap, while staying well below the top currency bar even with all 4 bags shown.
    private const float BottomStart = 500f;

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
        // Scale with screen size (ref 1080x1920, match 0.5) so our reference-pixel layout stays
        // aligned with the map-nav buttons / top bar on any device resolution — not the old
        // ConstantPixelSize, which drifted out of alignment as the render resolution changed.
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
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

    // Text colors tuned for legibility on the light wooden frame.
    private static readonly Color CountInk = new Color(0.20f, 0.14f, 0.09f);   // dark brown: seeds remaining
    private static readonly Color RedColor = new Color(0.75f, 0.13f, 0.10f);   // deep red: out of seeds / can't afford
    private static readonly Color PriceGreen = new Color(0.10f, 0.45f, 0.16f); // deep green: bag price when affordable

    // Cream/brown count badge (top-right corner).
    private static readonly Color BadgeCream = new Color(0.98f, 0.93f, 0.78f); // warm cream fill
    private static readonly Color BadgeRing  = new Color(0.30f, 0.20f, 0.11f); // dark brown ring

    // A soft-edged white circle sprite built once and tinted for both badge layers. Cached so every
    // bag reuses the same texture.
    private static Sprite _circleSprite;
    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size / 2f;
        var center = new Vector2(r, r);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(r - d); // ~1px anti-aliased edge
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _circleSprite;
    }

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
            bag.count.color = remaining <= 0 ? RedColor : CountInk;
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
            kv.Value.price.color = canBuy ? PriceGreen : RedColor;
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
        bag.count.color = remaining <= 0 ? RedColor : CountInk;
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

        // Framed panel behind the icon/text — the wooden "slot" look from the top-bar buttons, so
        // the icon and numbers read clearly against the busy farm behind them. Falls back to a flat
        // dark panel when no frame sprite is wired.
        var bgGo = new GameObject("bg", typeof(RectTransform));
        bgGo.transform.SetParent(root, false);
        var bgImg = bgGo.AddComponent<Image>();
        if (frameSprite != null)
        {
            bgImg.sprite = frameSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        }
        bgImg.raycastTarget = false;
        Stretch(bgGo.GetComponent<RectTransform>());

        // Seed-packet icon (fallback to crop sprite). Fills the upper portion, leaving a bottom band
        // for the price. The seed count now lives in a corner badge, not under the icon.
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
            irt.sizeDelta = new Vector2(WidgetW - 28f, WidgetH - 54f);
            irt.anchoredPosition = new Vector2(0f, -10f);
        }

        // Seeds-remaining badge: a cream disc with a dark-brown ring, pinned to the top-right corner
        // (notification-dot placement) so the number stays legible on a high-contrast background no
        // matter what's behind the widget. Two stacked circles give the ring; the number sits on top.
        var badgeGo = new GameObject("countBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(root, false);
        var badgeRing = badgeGo.AddComponent<Image>();
        badgeRing.sprite = CircleSprite();
        badgeRing.color = BadgeRing;
        badgeRing.raycastTarget = false;
        var brt = badgeGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        brt.anchoredPosition = new Vector2(-BadgeSize * 0.32f, -BadgeSize * 0.32f);

        var fillGo = new GameObject("fill", typeof(RectTransform));
        fillGo.transform.SetParent(badgeGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = CircleSprite();
        fillImg.color = BadgeCream;
        fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(4f, 4f); frt.offsetMax = new Vector2(-4f, -4f);

        var countGo = new GameObject("count", typeof(RectTransform));
        countGo.transform.SetParent(badgeGo.transform, false);
        var count = countGo.AddComponent<TextMeshProUGUI>();
        count.enableAutoSizing = true;      // shrink so 2–3 digit counts still fit the disc
        count.fontSizeMin = 14f;
        count.fontSizeMax = 26f;
        count.fontStyle = FontStyles.Bold;
        count.alignment = TextAlignmentOptions.Center;
        count.raycastTarget = false;
        count.color = CountInk;
        if (font != null) count.font = font;
        var crt = countGo.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(2f, 2f); crt.offsetMax = new Vector2(-2f, -2f);

        // Live (escalating) bag price across the bottom band (lifted off the very edge so it fits).
        var priceGo = new GameObject("price", typeof(RectTransform));
        priceGo.transform.SetParent(root, false);
        var price = priceGo.AddComponent<TextMeshProUGUI>();
        price.fontSize = 26;
        price.fontStyle = FontStyles.Bold;
        price.alignment = TextAlignmentOptions.Center;
        price.raycastTarget = false;
        price.color = PriceGreen;
        if (font != null) price.font = font;
        var prt = priceGo.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 0f);
        prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(0f, 34f);
        prt.anchoredPosition = new Vector2(0f, 12f);

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
