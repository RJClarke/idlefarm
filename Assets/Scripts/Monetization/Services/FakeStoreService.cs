#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor/Dev stand-in store: a "[Test Store]" confirm dialog (Confirm / Fail / Cancel).
/// Owned permanent unlocks live in PlayerPrefs as the fake "store account", so Restore and the
/// launch-time ownership check behave like a real store after the save is wiped.</summary>
public sealed class FakeStoreService : IStoreService
{
    private const string OwnedKey = "fake_store_owned";

    public bool IsInitialized { get; private set; }
    public event Action<PurchaseResult> OnPendingPurchase;

    public void Initialize(Action<bool> onDone) { IsInitialized = true; onDone?.Invoke(true); }

    public string GetPriceString(string productId) => null; // Store shows the catalog fallback

    public void Purchase(string productId, Action<PurchaseResult> onResult)
    {
        StoreProductDef def = StoreCatalogSO.Instance.Get(productId);
        string title = def != null ? def.displayName : productId;
        string price = def != null ? def.fallbackPrice : "";
        MonetizationDevOverlay.ShowPurchase(title, price, outcome =>
        {
            var result = new PurchaseResult { productId = productId, outcome = outcome };
            if (outcome == PurchaseOutcome.Success)
            {
                result.transactionId = "fake-" + Guid.NewGuid().ToString("N");
                if (def != null && def.kind == ProductKind.NonConsumable) AddOwned(productId);
            }
            onResult?.Invoke(result);
        });
    }

    public void ConfirmDelivered(string transactionId) { }

    public void Restore(Action<bool> onDone)
    {
        foreach (string id in Owned())
            OnPendingPurchase?.Invoke(new PurchaseResult { productId = id, transactionId = "fake-restore-" + id, outcome = PurchaseOutcome.Success });
        onDone?.Invoke(true);
    }

    public bool? IsOwned(string productId) => Owned().Contains(productId);

    /// <summary>Settings > Dev: forget every fake purchase (with StoreManager.DevResetPass).</summary>
    public static void DevClearAccount() { PlayerPrefs.DeleteKey(OwnedKey); PlayerPrefs.Save(); }

    private static HashSet<string> Owned()
    {
        string raw = PlayerPrefs.GetString(OwnedKey, "");
        return new HashSet<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void AddOwned(string id)
    {
        HashSet<string> owned = Owned();
        owned.Add(id);
        PlayerPrefs.SetString(OwnedKey, string.Join("|", owned));
        PlayerPrefs.Save();
    }
}
#endif
