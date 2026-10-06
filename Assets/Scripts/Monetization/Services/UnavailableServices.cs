using System;

/// <summary>Release builds without an ad SDK: never ready, never rewards.</summary>
public sealed class UnavailableAdService : IAdService
{
    public bool IsReady => false;
    public void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed) => onFailed?.Invoke(AdFailReason.NotReady);
}

/// <summary>Release builds without a store SDK: every purchase is Unavailable; nothing is ever granted.</summary>
public sealed class UnavailableStoreService : IStoreService
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
