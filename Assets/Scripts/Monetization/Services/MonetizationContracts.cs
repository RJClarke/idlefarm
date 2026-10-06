using System;

public enum AdFailReason { NotReady, Closed, Error }

/// <summary>Rewarded video ads. onRewarded fires only when the reward is earned (ad finished);
/// onFailed(Closed) when the player closes early, onFailed(NotReady) when there is no ad to show.</summary>
public interface IAdService
{
    bool IsReady { get; }
    void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed);
}

public enum PurchaseOutcome { Success, Cancelled, Failed, Unavailable }

public struct PurchaseResult
{
    public string productId;
    public string transactionId;
    public PurchaseOutcome outcome;
}

/// <summary>Real-money store. Delivery contract: grant + save, THEN ConfirmDelivered(transactionId).
/// Purchases the store re-sends (interrupted, restored) arrive via OnPendingPurchase.</summary>
public interface IStoreService
{
    bool IsInitialized { get; }
    event Action<PurchaseResult> OnPendingPurchase;
    void Initialize(Action<bool> onDone);
    /// <summary>Localized price, or null when unknown (caller shows the catalog fallback).</summary>
    string GetPriceString(string productId);
    void Purchase(string productId, Action<PurchaseResult> onResult);
    void ConfirmDelivered(string transactionId);
    void Restore(Action<bool> onDone);
    /// <summary>True/false from the store account; null when the store can't say (offline).</summary>
    bool? IsOwned(string productId);
}
