using System;
using UnityEngine;

/// <summary>One-shot narrative ledger + the player's farm/account name. Everything that
/// must happen "only the first time" checks HasFired/MarkFired here. Persisted via
/// SaveManager/GameData following the NewContentTracker LoadState/GetForSave pattern.</summary>
[DefaultExecutionOrder(1100)]
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    private readonly NarrativeLedger ledger = new NarrativeLedger();
    private string farmName = "";

    public event Action OnFarmNameChanged;

    public string FarmName => farmName;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public bool HasFired(string id) => ledger.HasFired(id);

    public bool MarkFired(string id)
    {
        bool isNew = ledger.MarkFired(id);
        if (isNew) SaveManager.Instance?.SaveGame();
        return isNew;
    }

    public void SetFarmName(string name)
    {
        // global:: reaches the static FarmName class; the unqualified name would bind to
        // this type's FarmName property (a string) and not resolve Sanitize.
        string clean = global::FarmName.Sanitize(name);
        if (clean == farmName) return;
        farmName = clean;
        SaveManager.Instance?.SaveGame();
        OnFarmNameChanged?.Invoke();
    }

    // ── Persistence (called by SaveManager) ──
    public void LoadState(string savedFarmName, string[] firedFlags)
    {
        farmName = savedFarmName ?? "";
        ledger.Load(firedFlags);
        OnFarmNameChanged?.Invoke();
    }

    public string GetFarmNameForSave() => farmName;
    public string[] GetFiredFlagsForSave() => ledger.ToArray();
}
