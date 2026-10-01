using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Two-segment COLLECT | SELL control driving <see cref="ItemInventoryManager.CollectMode"/>.
///
/// Replaces the old in-run-only button that RunUI cloned from the Start Run CTA. That button was
/// gated to <c>inRun &amp;&amp; atFarm</c> because it shared Start Run's anchor slot, which meant the
/// mode could not be switched out of a run — and animal eggs mature on a wall-clock timer regardless
/// of run state (AnimalManager.Update), so eggs claimed in town always fell through to the coin
/// payout and Reputation quests requiring eggs were unsatisfiable.
///
/// Sits directly under the top-right currency stack. Note the currency stack is UI Toolkit
/// (TopBarUITK) while this is uGUI, so the two can't share a layout container — the vertical
/// position is matched numerically, the same convention DevToolsSetup/QuestDebug already use via
/// their CURRENCY_CLEARANCE constant. Those two dev buttons sit below this control and offset
/// themselves by COLLECT_TOGGLE_CLEARANCE to make room.
///
/// Attach to a bare child of the main HUD Canvas. This component builds its own segments as a
/// container at runtime and toggles THAT container's visibility, so the component itself stays
/// enabled and keeps receiving camera-pan events.
/// </summary>
public class CollectModeToggle : MonoBehaviour
{
    [Tooltip("Optional. TMP font for the segment labels. Falls back to a font copied from any " +
             "TextMeshProUGUI already in this canvas, then to the TMP default.")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Tooltip("Optional. Canvas to build into. Defaults to the Canvas above this object.")]
    [SerializeField] private RectTransform host;

    // Width matches the dev-button column so the right edge reads as one stack. Vertical slot is
    // the one the Dev Tools button used to occupy, directly below the 4-row currency stack.
    private const float PanelWidth = 260f;
    private const float PanelHeight = 64f;
    private const float Margin = 20f;
    private const float CurrencyClearance = 300f;
    private const float SegmentGap = 6f;
    private const float LabelFontSize = 20f;

    // These tint the wood FRAME sprite, not a fill behind it. UI_Wood_Slot_Available_01_0 is an
    // opaque panel rather than a transparent-center frame, so anything drawn behind it is invisible
    // — the frame is the only visible surface. Values are therefore pre-divided by the sprite's own
    // tan (~0.85, 0.75, 0.60) so that sprite x tint lands on the intended final color.
    private static readonly Color CollectTint  = new Color(0.52f, 0.88f, 0.50f); // -> mid green
    private static readonly Color SellTint     = new Color(0.95f, 0.88f, 0.35f); // -> warm amber
    private static readonly Color InactiveTint = new Color(0.38f, 0.34f, 0.29f); // -> dark brown

    // Contrast rule: the two ACTIVE surfaces come out light/saturated, where near-black reads
    // strongest (~6.5:1 on the green, ~9.8:1 on the amber — white would only reach ~3.5:1 on the
    // green). The INACTIVE surface is dark brown, where black would be unreadable, so that one gets
    // cream (~6.7:1). Every state is legible, and light-on-dark vs dark-on-light also reinforces
    // which segment is selected.
    private static readonly Color TextOnGreen    = new Color(0.06f, 0.14f, 0.05f);
    private static readonly Color TextOnGold     = new Color(0.16f, 0.11f, 0.02f);
    private static readonly Color TextOnInactive = new Color(0.90f, 0.85f, 0.74f);

    // Base drawn behind the frame — only visible if the frame sprite is missing.
    private static readonly Color BaseFill = new Color(0.32f, 0.23f, 0.15f);

    private GameObject container;

    /// <summary>The switch's panel, for tutorial spotlighting (null before BuildUI).</summary>
    public RectTransform TargetRect => container != null ? (RectTransform)container.transform : null;
    private Image collectFill, sellFill;
    private TextMeshProUGUI collectLabel, sellLabel;
    private CameraPanController pan;

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        // Subscribe in Start, not Awake/OnEnable: ItemInventoryManager.Instance is assigned in its
        // own Awake and manager order is undefined, so it may still be null when we Awake.
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnCollectModeChanged += OnCollectModeChanged;

        pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (pan != null)
        {
            pan.OnPanStarted   += ApplyVisibility;
            pan.OnPanCompleted += ApplyVisibility;
        }

        RefreshVisual();
        ApplyVisibility(pan != null ? pan.CurrentLocation : CameraPanController.Location.Farm);
    }

    private void OnDestroy()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.OnCollectModeChanged -= OnCollectModeChanged;
        if (pan != null)
        {
            pan.OnPanStarted   -= ApplyVisibility;
            pan.OnPanCompleted -= ApplyVisibility;
        }
    }

    // ── Visibility ────────────────────────────────────────────────────────

    /// <summary>
    /// Hidden at the Market only — shown everywhere output can be claimed, in-run or out. Note this
    /// reads the event's location ARGUMENT rather than pan.CurrentLocation: OnPanStarted fires with
    /// the destination while CurrentLocation only flips on completion, so using the argument settles
    /// visibility at the start of the pan instead of one frame late.
    /// </summary>
    private void ApplyVisibility(CameraPanController.Location loc)
    {
        if (container != null)
            container.SetActive(loc != CameraPanController.Location.Market);
    }

    // ── Mode switching ────────────────────────────────────────────────────

    // Each segment SETS its mode rather than inverting the flag. Tapping the already-active segment
    // is a no-op, so a mis-tap can't silently redirect farm output to the other mode.
    private void SelectCollect() => SetMode(true);
    private void SelectSell()    => SetMode(false);

    private void SetMode(bool collect)
    {
        if (ItemInventoryManager.Instance == null) return;
        ItemInventoryManager.Instance.CollectMode = collect; // setter early-returns if unchanged
    }

    private void OnCollectModeChanged(bool _) => RefreshVisual();

    private void RefreshVisual()
    {
        bool collect = ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.CollectMode;

        if (collectFill != null) collectFill.color = collect ? CollectTint : InactiveTint;
        if (sellFill != null)    sellFill.color    = collect ? InactiveTint : SellTint;

        if (collectLabel != null)
        {
            collectLabel.color = collect ? TextOnGreen : TextOnInactive;
            collectLabel.fontStyle = collect ? FontStyles.Bold : FontStyles.Normal;
        }
        if (sellLabel != null)
        {
            sellLabel.color = collect ? TextOnInactive : TextOnGold;
            sellLabel.fontStyle = collect ? FontStyles.Normal : FontStyles.Bold;
        }
    }

    // ── Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        RectTransform parent = ResolveHost();
        if (parent == null)
        {
            Debug.LogWarning("[CollectModeToggle] No canvas found to build into.");
            return;
        }

        container = new GameObject("CollectSellToggle", typeof(RectTransform));
        RectTransform crt = container.GetComponent<RectTransform>();
        crt.SetParent(parent, false);

        // Top-right, hugging the bottom of the currency stack.
        crt.anchorMin = new Vector2(1f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot     = new Vector2(1f, 1f);
        crt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        crt.anchoredPosition = new Vector2(-Margin, -(Margin + CurrencyClearance));

        HorizontalLayoutGroup row = container.AddComponent<HorizontalLayoutGroup>();
        row.spacing = SegmentGap;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        Sprite frame = FindFrameSprite();
        TMP_FontAsset font = ResolveFont();

        BuildSegment("SegmentCollect", "COLLECT", frame, font, SelectCollect,
                     out collectFill, out collectLabel);
        BuildSegment("SegmentSell", "SELL", frame, font, SelectSell,
                     out sellFill, out sellLabel);
    }

    private void BuildSegment(string name, string text, Sprite frame, TMP_FontAsset font,
                              UnityEngine.Events.UnityAction onClick,
                              out Image fill, out TextMeshProUGUI label)
    {
        GameObject seg = new GameObject(name, typeof(RectTransform));
        seg.transform.SetParent(container.transform, false);

        // Root image is only a fallback backing — the wood frame on top is opaque and covers it.
        Image root = seg.AddComponent<Image>();
        root.color = BaseFill;

        Button btn = seg.AddComponent<Button>();
        btn.targetGraphic = root;
        btn.onClick.AddListener(onClick);

        // The frame is the visible surface, so it is what carries the state color.
        fill = root;
        if (frame != null)
        {
            GameObject frameGO = new GameObject("Frame", typeof(RectTransform));
            RectTransform frt = frameGO.GetComponent<RectTransform>();
            frt.SetParent(seg.transform, false);
            Stretch(frt);
            Image fimg = frameGO.AddComponent<Image>();
            fimg.sprite = frame;
            fimg.type = frame.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            fimg.raycastTarget = false;
            fill = fimg;
        }

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.SetParent(seg.transform, false);
        Stretch(lrt);
        lrt.offsetMin = new Vector2(4f, 4f);
        lrt.offsetMax = new Vector2(-4f, -4f);

        label = labelGO.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;                       // plain ASCII: emoji render invisible on Android
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        // NoWrap + a FIXED size, deliberately not auto-sizing. TMP's auto-size will not shrink text
        // to satisfy a width constraint while overflowMode is Overflow, so "COLLECT" settled at 25pt
        // needing 129px inside a 115px segment and spilled past the frame. 20pt measures ~92px, which
        // clears the segment with room to spare, and a fixed size also keeps both labels the same
        // size instead of letting the shorter word render larger.
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = false;
        label.fontSize = LabelFontSize;
        label.color = TextOnInactive;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform ResolveHost()
    {
        if (host != null) return host;
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.GetComponent<RectTransform>() : null;
    }

    /// <summary>Borrow the wood slot frame from the HUD's InventoryButton so the segments match the
    /// existing button styling without hardcoding an asset path.</summary>
    private Sprite FindFrameSprite()
    {
        RectTransform parent = ResolveHost();
        if (parent == null) return null;

        // Search the whole canvas (including inactive) — the lockup is no longer our parent.
        foreach (Image img in parent.GetComponentsInChildren<Image>(true))
            if (img.name == "SlotFrame" && img.sprite != null) return img.sprite;
        return null;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (labelFont != null) return labelFont;

        // Match whatever the rest of this canvas already uses.
        RectTransform parent = ResolveHost();
        if (parent != null)
        {
            TextMeshProUGUI sample = parent.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sample != null && sample.font != null) return sample.font;
        }
        return TMP_Settings.defaultFontAsset;
    }
}
