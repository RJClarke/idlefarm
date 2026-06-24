using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns received letters. Public Deliver(id) lets any system drop a letter in.
/// Content is resolved from the authored LetterCatalogSO; state is the pure InboxModel,
/// persisted via SaveManager/GameData.</summary>
[DefaultExecutionOrder(1100)]
public class InboxManager : MonoBehaviour
{
    public static InboxManager Instance { get; private set; }

    [SerializeField] private LetterCatalogSO catalog;

    private readonly InboxModel model = new InboxModel();

    public event Action OnInboxChanged;

    public LetterCatalogSO Catalog => catalog;
    public IReadOnlyList<InboxEntry> Entries => model.Entries;
    public int UnreadCount() => model.UnreadCount();
    public LetterDef GetDef(string letterId) => catalog != null ? catalog.Get(letterId) : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Append a letter (must exist in the catalog) and notify listeners.</summary>
    public void Deliver(string letterId)
    {
        if (catalog == null || catalog.Get(letterId) == null)
        {
            Debug.LogWarning($"[InboxManager] Deliver: unknown letterId '{letterId}'.");
            return;
        }
        model.Deliver(letterId, DateTime.UtcNow.Ticks);
        SaveManager.Instance?.SaveGame();
        OnInboxChanged?.Invoke();
        Debug.Log($"[InboxManager] Delivered letter '{letterId}'. Unread: {model.UnreadCount()}");
    }

    public void MarkRead(string letterId)
    {
        if (model.MarkRead(letterId))
        {
            SaveManager.Instance?.SaveGame();
            OnInboxChanged?.Invoke();
        }
    }

    /// <summary>Grants the letter's reward exactly once. Returns true iff a reward was granted.</summary>
    public bool ClaimReward(string letterId)
    {
        var def = GetDef(letterId);
        if (def == null || def.rewardKind == RewardKind.None || def.rewardAmount <= 0) return false;
        if (!model.Claim(letterId)) return false; // already claimed

        switch (def.rewardKind)
        {
            case RewardKind.Coins:   CurrencyManager.Instance?.AddCoins(def.rewardAmount); break;
            case RewardKind.Gems:    CurrencyManager.Instance?.AddGems(def.rewardAmount); break;
            case RewardKind.Compost: CurrencyManager.Instance?.AddCompost(def.rewardAmount); break;
        }
        SaveManager.Instance?.SaveGame();
        OnInboxChanged?.Invoke();
        return true;
    }

    // ── Persistence (called by SaveManager) ──
    public void LoadState(InboxEntry[] entries)
    {
        model.Load(entries);
        OnInboxChanged?.Invoke();
    }

    public InboxEntry[] GetForSave() => model.ToArray();
}
