using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Skin ownership + equip (spec 2026-10-05 §5). Gem purchases spend here, real-money sets grant via
/// StoreManager. Saves in GameData. Self-bootstrapping per scene with a save (like StoreManager); attaches the
/// farmhouse applier on Start. Animal visuals get a SkinSwapper from AnimalManager.</summary>
public sealed class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }
    public event Action OnSkinsChanged;

    private SkinOwnershipCore core;
    private SkinCatalogSO catalog;

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
        new GameObject("SkinManager").AddComponent<SkinManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        catalog = SkinCatalogSO.Instance;
        core = new SkinOwnershipCore(catalog.skins);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        BarnBuilding house = FindFirstObjectByType<BarnBuilding>(FindObjectsInactive.Include);
        if (house != null && house.GetComponent<BuildingSkinApplier>() == null)
            house.gameObject.AddComponent<BuildingSkinApplier>().Init("farmhouse");
    }

    public IReadOnlyList<SkinDef> All => catalog.skins;
    public SkinDef Get(string id) => catalog.Get(id);
    public bool IsOwned(string id) => core.IsOwned(id);
    public bool IsEquipped(string id) => core.IsEquipped(id);
    public int PriceOf(string id) => core.PriceOf(id);

    /// <summary>The equipped skin's def for a target, or null for Classic.</summary>
    public SkinDef EquippedDef(string target)
    {
        string id = core.EquippedFor(target);
        return SkinDefaults.IsClassic(id) ? null : catalog.Get(id);
    }

    public SkinBuyResult TryBuy(string id)
    {
        CurrencyManager cm = CurrencyManager.Instance;
        SkinBuyResult r = core.CheckBuy(id, cm != null ? cm.Gems : 0);
        if (r != SkinBuyResult.Ok) return r;
        int price = core.PriceOf(id);
        if (!cm.SpendGems(price)) return SkinBuyResult.NotEnoughGems;
        core.MarkBought(id);
        Debug.Log($"[Skins] Bought + equipped {id} for {price} gems");
        Changed();
        return r;
    }

    public bool Equip(string id)
    {
        if (!core.Equip(id)) return false;
        Changed();
        return true;
    }

    public void Grant(string[] ids)
    {
        core.Grant(ids);
        Changed();
    }

    private void Changed()
    {
        SaveManager.Instance?.SaveGame();
        OnSkinsChanged?.Invoke();
    }

    public void CaptureTo(GameData d)
    {
        d.ownedSkinIds = core.ExportOwned();
        d.equippedSkins = core.ExportEquipped();
    }

    public void LoadFrom(GameData d)
    {
        core.Import(d.ownedSkinIds, d.equippedSkins);
        OnSkinsChanged?.Invoke();
    }

    public void DevResetSkins()
    {
        core.Import(null, null);
        Changed();
    }
}
