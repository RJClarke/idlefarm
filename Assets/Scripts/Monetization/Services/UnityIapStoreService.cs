#if UNITY_PURCHASING
using System;

/// <summary>
/// PHASE 2 STUB, compiled only when UNITY_PURCHASING is defined (Unity IAP installed). Implement with
/// the unity:implement-in-app-purchases skill:
///   - Initialize: register every StoreCatalogSO product (Consumable / NonConsumable) and init IAP;
///   - GetPriceString: the product's localized price string;
///   - Purchase/ProcessPurchase: validate the receipt locally (CrossPlatformValidator), raise the result
///     (or OnPendingPurchase for re-sent purchases) and leave it PENDING; StoreManager grants + saves,
///     then calls ConfirmDelivered(transactionId) -> ConfirmPendingPurchase;
///   - Restore: Google restores automatically on init; iOS needs RestoreTransactions;
///   - IsOwned: hasReceipt for NonConsumables, null before init.
/// Until then it behaves like UnavailableStoreService.
/// </summary>
public sealed class UnityIapStoreService : IStoreService
{
    public bool IsInitialized => false;
    public event Action<PurchaseResult> OnPendingPurchase { add { } remove { } }
    public void Initialize(Action<bool> onDone) => onDone?.Invoke(false);
    public string GetPriceString(string productId) => null;
    public void Purchase(string productId, Action<PurchaseResult> onResult) =>
        onResult?.Invoke(new PurchaseResult { productId = productId, outcome = PurchaseOutcome.Unavailable });
    public void ConfirmDelivered(string transactionId) { }
    public void Restore(Action<bool> onDone) => onDone?.Invoke(false);
    public bool? IsOwned(string productId) => null;
}
#endif
