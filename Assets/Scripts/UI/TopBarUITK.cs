using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit top-bar currency stack. Hugs the top-right corner under the Dev Tools
/// button and shows Money / Coins / Gems / Compost. Subscribes to CurrencyManager
/// events and punch-scales each label on change.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(500)]
public class TopBarUITK : MonoBehaviour
{
    public static TopBarUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root;
    private Label moneyLabel;
    private Label coinsLabel;
    private Label gemsLabel;
    private Label compostLabel;

    private bool subscribed;
    private bool gemsRowWired;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        Cache();
        TrySubscribe();
        // Initial values (deferred to next frame so CurrencyManager has loaded saved state).
        if (root != null) root.schedule.Execute(SyncAll).StartingIn(50);
    }

    private void OnDisable() => Unsubscribe();

    private void Cache()
    {
        if (document == null) document = GetComponent<UIDocument>();
        root = document != null ? document.rootVisualElement : null;
        if (root == null) return;
        // Root lets scene clicks through around the pills; the currency rows themselves are
        // picking-mode="Position" in UXML so a tap ON a pill can't fall through to the world
        // (it used to reel in the fishing line).
        root.pickingMode = PickingMode.Ignore;
        moneyLabel   = root.Q<Label>("money-value");
        coinsLabel   = root.Q<Label>("coins-value");
        gemsLabel    = root.Q<Label>("gems-value");
        compostLabel = root.Q<Label>("compost-value");

        // Tapping the gem counter opens the Store (guarded: OnEnable re-runs Cache).
        VisualElement gemsRow = root.Q<VisualElement>("row-gems");
        if (gemsRow != null && !gemsRowWired)
        {
            gemsRow.RegisterCallback<ClickEvent>(_ => StorePopupUITK.Open());
            gemsRowWired = true;
        }
    }

    /// <summary>Centre of a currency row ("row-gems", "row-coins") as 0..1 of the screen from the
    /// top-left, so another panel can fly a reward into it whatever its own scale.</summary>
    public bool TryGetRowCenterNormalized(string rowName, out Vector2 normalized)
    {
        normalized = default;
        if (root == null) return false;
        VisualElement row = root.Q<VisualElement>(rowName);
        Rect panel = root.worldBound;
        if (row == null || panel.width <= 0f || panel.height <= 0f) return false;
        Vector2 c = row.worldBound.center;
        normalized = new Vector2((c.x - panel.x) / panel.width, (c.y - panel.y) / panel.height);
        return true;
    }

    private void TrySubscribe()
    {
        if (subscribed || CurrencyManager.Instance == null) return;
        CurrencyManager.Instance.OnMoneyChanged   += OnMoney;
        CurrencyManager.Instance.OnCoinsChanged   += OnCoins;
        CurrencyManager.Instance.OnGemsChanged    += OnGems;
        CurrencyManager.Instance.OnCompostChanged += OnCompost;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || CurrencyManager.Instance == null) { subscribed = false; return; }
        CurrencyManager.Instance.OnMoneyChanged   -= OnMoney;
        CurrencyManager.Instance.OnCoinsChanged   -= OnCoins;
        CurrencyManager.Instance.OnGemsChanged    -= OnGems;
        CurrencyManager.Instance.OnCompostChanged -= OnCompost;
        subscribed = false;
    }

    private void SyncAll()
    {
        if (!subscribed) TrySubscribe();
        var cm = CurrencyManager.Instance;
        if (cm == null) return;
        SetMoney(cm.Money);
        SetCoins(cm.Coins);
        SetGems(cm.Gems);
        SetCompost(cm.Compost);
    }

    private void OnMoney(int v)   => SetMoney(v);
    private void OnCoins(int v)   => SetCoins(v);
    private void OnGems(int v)    => SetGems(v);
    private void OnCompost(int v) => SetCompost(v);

    private void SetMoney(int v)   { if (moneyLabel   != null) { moneyLabel.text   = Format(v);       Pulse(moneyLabel); } }
    private void SetCoins(int v)   { if (coinsLabel   != null) { coinsLabel.text   = Format(v);       Pulse(coinsLabel); } }
    private void SetGems(int v)    { if (gemsLabel    != null) { gemsLabel.text    = Format(v);       Pulse(gemsLabel); } }
    private void SetCompost(int v) { if (compostLabel != null) { compostLabel.text = Format(v);       Pulse(compostLabel); } }

    private static string Format(int v) => v.ToString("N0");

    // Quick 1.0 → 1.15 → 1.0 punch on the label to call out the change.
    private static void Pulse(VisualElement el)
    {
        el.transform.scale = new Vector3(1.15f, 1.15f, 1f);
        el.schedule.Execute(() => el.transform.scale = Vector3.one).StartingIn(120);
    }
}
