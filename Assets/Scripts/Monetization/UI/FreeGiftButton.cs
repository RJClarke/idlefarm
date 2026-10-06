using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD Free Gift chest, in a fixed slot above the egg/gem claim button (baked by Farm Game >
/// Monetization > Bake Gift Button). Ready: pulse + dot, corner tag FREE / AD (none with the pass).
/// Cooldown: dim + mm:ss. Capped: dim + "Tomorrow". Locked: invisible (CanvasGroup, never deactivated,
/// because LocationModeController toggles this GameObject at the Market).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FreeGiftButton : MonoBehaviour
{
    public static FreeGiftButton Instance { get; private set; }

    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject notificationDot;
    [SerializeField] private TextMeshProUGUI timerLabel;
    [SerializeField] private GameObject tagBadge;
    [SerializeField] private TextMeshProUGUI tagLabel;

    private static readonly Color DimColor = new Color(0.6f, 0.6f, 0.6f, 0.85f);

    private CanvasGroup group;
    private float nextRefresh;
    private bool pulsing, introDone, subscribed;

    public RectTransform Rect => (RectTransform)transform;
    public bool IsShown => group != null && group.alpha > 0.5f && gameObject.activeInHierarchy;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        if (subscribed && FreeGiftManager.Instance != null) FreeGiftManager.Instance.OnStateChanged -= Refresh;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (button != null) button.onClick.AddListener(() => FreeGiftManager.Instance?.RequestClaim());
        // Same round dot as the egg button (it builds its circle at runtime; the baked clone has none).
        Image dotImage = notificationDot != null ? notificationDot.GetComponent<Image>() : null;
        if (dotImage != null) dotImage.sprite = EggClaimButton.BuildCircleSprite(64);
        TrySubscribe();
        Refresh();
    }

    private void TrySubscribe()
    {
        if (subscribed || FreeGiftManager.Instance == null) return;
        FreeGiftManager.Instance.OnStateChanged += Refresh;
        subscribed = true;
    }

    private void OnEnable() { nextRefresh = 0f; pulsing = false; }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        TrySubscribe();
        Refresh();
    }

    private void Refresh()
    {
        FreeGiftManager m = FreeGiftManager.Instance;
        GiftStatus status = m != null ? m.Status : GiftStatus.Locked;
        bool shown = status != GiftStatus.Locked;
        group.alpha = shown ? 1f : 0f;
        group.interactable = shown;
        group.blocksRaycasts = shown;
        if (!shown) { SetPulse(false); return; }

        bool ready = status == GiftStatus.Ready;
        if (icon != null) icon.color = ready ? Color.white : DimColor;
        if (notificationDot != null) notificationDot.SetActive(ready);
        if (timerLabel != null)
        {
            timerLabel.gameObject.SetActive(!ready);
            timerLabel.text = status == GiftStatus.Capped ? "Tomorrow" : MonetizationUI.Mmss(m.SecondsUntilReady);
        }
        bool pass = FreeGiftManager.PassOwned();
        bool showTag = ready && !pass;
        if (tagBadge != null) tagBadge.SetActive(showTag);
        if (tagLabel != null && showTag) tagLabel.text = m.NextClaimNeedsAd ? "AD" : "FREE";
        SetPulse(ready);

        // Retried every refresh (0.5s) until shown or completed: an onboarding step or the open
        // mailbox can block it, and the intro must not be lost.
        if (!introDone) introDone = m.TryShowIntro();
    }

    private void SetPulse(bool on)
    {
        if (on == pulsing) return;
        pulsing = on;
        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one;
        if (on) LeanTween.scale(gameObject, Vector3.one * 1.08f, 0.6f).setEaseInOutSine().setLoopPingPong();
    }
}
