using System;
using UnityEngine;

/// <summary>
/// The Pantry: fungible counted stacks of raw and smoked fish (spec §1). Mirrors the
/// CurrencyManager/Compost pattern — integers + change events, no item objects. Fish are the
/// inter-building resource: caught at the Lake, stored here, pulled into the Smokehouse, and the
/// smoked output lands back here as counts. Tiers are 1-based in the API; stored 0-based.
/// </summary>
public class PantryManager : MonoBehaviour
{
    public static PantryManager Instance { get; private set; }

    private readonly int[] raw = new int[FishTiers.Count];
    private readonly int[] smoked = new int[FishTiers.Count];

    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static int Idx(int tier) => Mathf.Clamp(tier, 1, FishTiers.Count) - 1;

    public int GetRaw(int tier) => raw[Idx(tier)];
    public int GetSmoked(int tier) => smoked[Idx(tier)];

    public int TotalRaw { get { int n = 0; for (int i = 0; i < raw.Length; i++) n += raw[i]; return n; } }
    public int TotalSmoked { get { int n = 0; for (int i = 0; i < smoked.Length; i++) n += smoked[i]; return n; } }

    public void AddRaw(int tier)    { raw[Idx(tier)]++;    OnChanged?.Invoke(); }
    public void AddSmoked(int tier) { smoked[Idx(tier)]++; OnChanged?.Invoke(); }

    public bool SpendRaw(int tier)
    {
        int i = Idx(tier);
        if (raw[i] <= 0) return false;
        raw[i]--;
        OnChanged?.Invoke();
        return true;
    }

    public bool SpendSmoked(int tier)
    {
        int i = Idx(tier);
        if (smoked[i] <= 0) return false;
        smoked[i]--;
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureTo(GameData d)
    {
        d.pantryRawFish = (int[])raw.Clone();
        d.pantrySmokedFish = (int[])smoked.Clone();
    }

    public void LoadFrom(GameData d)
    {
        CopyInto(d.pantryRawFish, raw);
        CopyInto(d.pantrySmokedFish, smoked);
        OnChanged?.Invoke();
    }

    private static void CopyInto(int[] src, int[] dst)
    {
        for (int i = 0; i < dst.Length; i++)
            dst[i] = (src != null && i < src.Length) ? Mathf.Max(0, src[i]) : 0;
    }
}
