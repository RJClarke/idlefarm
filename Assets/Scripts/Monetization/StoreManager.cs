using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Real-money purchases (spec 2026-10-04 §6/§8/§9, 2026-10-05 §4). Delivery: ledger check -> grant -> save ->
/// confirm to the store, then the chest reveal. Permanent purchases (pass, sets, starter, Harvest Blessing) are
/// cached locally for offline play and re-checked against the store account at launch (store says not owned ->
/// cleared; store unreachable -> kept). Sets grant skins via SkinManager (idempotent, so restores re-grant).
/// Self-bootstrapping per scene, like FreeGiftManager.
/// </summary>
public sealed class StoreManager : MonoBehaviour
{
    private const string PassBanner = "Farmer's Pass unlocked!";

    public static StoreManager Instance { get; private set; }
    public event Action OnEntitlementsChanged;

    private readonly PurchaseLedgerCore ledger = new PurchaseLedgerCore();
    private readonly HashSet<string> ownedProducts = new HashSet<string>(StringComparer.Ordinal);
    private bool passOwned;  // legacy field (GameData.farmersPassOwned) kept for save compatibility
    private bool busy;
    private bool initStarted;
    private bool pendingSubscribed;

    public bool HasFarmersPass => passOwned;
    public bool Busy => busy;

    /// <summary>Harvest Coins multiplier from Harvest Blessing (1 when not owned).</summary>
    public static float HarvestCoinMultiplier =>
        Instance != null && Instance.IsProductOwned(StoreDefaults.HarvestBlessingId) ? StoreDefaults.HarvestBlessingMultiplier : 1f;

    /// <summary>Permanent purchases. The pass keeps its own legacy flag.</summary>
    public bool IsProductOwned(string productId) =>
        productId == StoreDefaults.PassId ? passOwned : (productId != null && ownedProducts.Contains(productId));

    private void SetProductOwned(string productId, bool owned)
    {
        if (productId == StoreDefaults.PassId) { passOwned = owned; return; }
        if (owned) ownedProducts.Add(productId); else ownedProducts.Remove(productId);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (Instance != null || SaveManager.Instance == null) return;
        new GameObject("StoreManager").AddComponent<StoreManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        FreeGiftManager.PassOwned = () => Instance != null && Instance.passOwned;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (pendingSubscribed) MonetizationServices.Store.OnPendingPurchase -= Deliver;
        Instance = null;
    }

    // After LoadGame (CurrencyManager.Start, order 2000) so a launch-time reconcile wins over the save.
    private void Start() => Invoke(nameof(InitStore), 0.5f);

    private void InitStore()
    {
        if (initStarted) return;
        initStarted = true;
        IStoreService store = MonetizationServices.Store;
        store.OnPendingPurchase += Deliver;
        pendingSubscribed = true;
        store.Initialize(ok =>
        {
            if (!ok) return;
            bool changed = false;
            foreach (StoreProductDef def in StoreCatalogSO.Instance.products)
            {
                if (def == null || def.comingSoon || def.kind != ProductKind.NonConsumable) continue;
                bool? owned = store.IsOwned(def.id);
                if (!owned.HasValue || owned.Value == IsProductOwned(def.id)) continue;
                SetProductOwned(def.id, owned.Value);
                if (owned.Value && def.grantsSkinIds != null && def.grantsSkinIds.Length > 0) SkinManager.Instance?.Grant(def.grantsSkinIds);
                Debug.Log($"[Store] {def.id} ownership reconciled with the store: {owned.Value}");
                changed = true;
            }
            if (!changed) return;
            SaveManager.Instance?.SaveGame();
            OnEntitlementsChanged?.Invoke();
            FreeGiftManager.Instance?.NotifyChanged();
        });
    }

    public string PriceFor(string productId)
    {
        string live = MonetizationServices.Store.GetPriceString(productId);
        if (!string.IsNullOrEmpty(live)) return live;
        StoreProductDef def = StoreCatalogSO.Instance.Get(productId);
        return def != null ? def.fallbackPrice : "";
    }

    public void Purchase(string productId)
    {
        StoreProductDef def = StoreCatalogSO.Instance.Get(productId);
        if (def == null || def.comingSoon || busy) return;
        if (def.kind == ProductKind.NonConsumable && IsProductOwned(productId)) return;
        busy = true;
        OnEntitlementsChanged?.Invoke();
        MonetizationServices.Store.Purchase(productId, result =>
        {
            busy = false;
            switch (result.outcome)
            {
                case PurchaseOutcome.Success: Deliver(result); break;
                case PurchaseOutcome.Failed: Toast("Purchase failed. You weren't charged."); break;
                case PurchaseOutcome.Unavailable: Toast("Store unavailable right now."); break;
                case PurchaseOutcome.Cancelled: break;
            }
            OnEntitlementsChanged?.Invoke();
        });
    }

    public void Restore()
    {
        MonetizationServices.Store.Restore(ok =>
            Toast(ok ? "Purchases restored." : "Couldn't restore. Try again later."));
    }

    /// <summary>Grant + save, THEN confirm. Runs for fresh purchases and store re-deliveries alike.</summary>
    private void Deliver(PurchaseResult result)
    {
        StoreProductDef def = StoreCatalogSO.Instance.Get(result.productId);
        if (def == null)
        {
            Debug.LogError($"[Store] Delivered unknown product '{result.productId}'. Confirming so it doesn't loop.");
            MonetizationServices.Store.ConfirmDelivered(result.transactionId);
            return;
        }

        bool isPass = def.id == StoreDefaults.PassId;
        bool permanent = def.kind == ProductKind.NonConsumable;
        bool ledgerNew = ledger.TryMarkDelivered(result.transactionId);
        bool grant = PurchaseLedgerCore.ShouldGrant(ledgerNew, permanent, permanent && IsProductOwned(def.id));

        if (permanent) SetProductOwned(def.id, true);
        if (isPass) FreeGiftManager.Instance?.MarkPitchShown();
        if (def.grantsSkinIds != null && def.grantsSkinIds.Length > 0) SkinManager.Instance?.Grant(def.grantsSkinIds); // idempotent: restores skins too
        if (grant && def.gems > 0) CurrencyManager.Instance?.AddGems(def.gems);
        Debug.Log($"[Store] Delivered {def.id} (tx {result.transactionId}) grant={grant}");

        SaveManager.Instance?.SaveGame();
        MonetizationServices.Store.ConfirmDelivered(result.transactionId);
        OnEntitlementsChanged?.Invoke();
        FreeGiftManager.Instance?.NotifyChanged(); // HUD tag drops "AD" once the pass is owned

        if (!grant) return;
        var request = new ChestRevealRequest { banner = isPass ? PassBanner : (permanent ? def.displayName + " unlocked!" : null) };
        if (def.gems > 0) request.lines.Add(new RewardLine(RewardCurrency.Gems, def.gems));
        ChestRevealUITK.Show(request); // granted already; the chest is the celebration
    }

    private static void Toast(string message) =>
        ToastManager.Show(message, null, ToastManager.ToastKind.Success, MonetizationUI.ChestIcon);

    public void CaptureTo(GameData d)
    {
        d.farmersPassOwned = passOwned;
        d.deliveredTransactionIds = ledger.Export();
        d.ownedProductIds = new List<string>(ownedProducts).ToArray();
    }

    public void LoadFrom(GameData d)
    {
        passOwned = d.farmersPassOwned;
        ledger.Import(d.deliveredTransactionIds);
        ownedProducts.Clear();
        if (d.ownedProductIds != null) foreach (string id in d.ownedProductIds) if (!string.IsNullOrEmpty(id)) ownedProducts.Add(id);
        OnEntitlementsChanged?.Invoke();
    }

    /// <summary>Settings > Dev: forget every permanent purchase locally AND in the fake store account.</summary>
    public void DevResetPass()
    {
        passOwned = false;
        ownedProducts.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        FakeStoreService.DevClearAccount();
#endif
        SaveManager.Instance?.SaveGame();
        OnEntitlementsChanged?.Invoke();
        FreeGiftManager.Instance?.NotifyChanged();
    }
}
