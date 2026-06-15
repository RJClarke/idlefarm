using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Tracks which unlockable content (research entries, equipment) the player has already
/// seen, so newly-available content can be surfaced with NEW badges + an unlock toast.
///
/// Content is "available" when it passes its visibility gates (research → IsResearchVisible,
/// equipment → feature flag + IsUnlocked). NEW = available AND not yet seen.
///
/// Data-driven: works for every current/future unlock, not just the scarecrow. Seen-state
/// persists in GameData. On first run (no saved state) everything currently available is
/// seeded as seen, so only content that opens up LATER lights up.
/// </summary>
[DefaultExecutionOrder(1100)]
public class NewContentTracker : MonoBehaviour
{
    public static NewContentTracker Instance { get; private set; }

    [Tooltip("Same EquipmentRegistry asset the EquipmentPopupUITK uses.")]
    [SerializeField] private EquipmentRegistry equipmentRegistry;

    private readonly HashSet<string> seen = new HashSet<string>();
    private readonly HashSet<string> available = new HashSet<string>();
    private bool seeded;
    private bool started;

    /// <summary>Fired whenever availability or seen-state changes; badge holders refresh on this.</summary>
    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        started = true;
        EnsureSeeded();
        Subscribe();
        Recompute(announce: false); // baseline; no toast on boot
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    // ── Persistence (called by SaveManager) ──────────

    public void LoadState(string[] savedSeen)
    {
        seen.Clear();
        if (savedSeen != null && savedSeen.Length > 0)
        {
            foreach (string s in savedSeen)
                if (!string.IsNullOrEmpty(s)) seen.Add(s);
            seeded = true; // had a saved set → don't re-seed
        }
        else
        {
            seeded = false; // new/legacy save → seed baseline below
        }

        if (started)
        {
            EnsureSeeded();
            Recompute(announce: false);
        }
    }

    public string[] GetSeenForSave() => seen.ToArray();

    // ── Queries (used by badges) ─────────────────────

    public bool IsNew(string contentId) =>
        !string.IsNullOrEmpty(contentId) && available.Contains(contentId) && !seen.Contains(contentId);

    public bool HasUnseenResearch() => available.Any(id => id.StartsWith("research:") && !seen.Contains(id));
    public bool HasUnseenEquipment() => available.Any(id => id.StartsWith("equip:") && !seen.Contains(id));

    public static string ResearchId(string researchID) => "research:" + researchID;
    public static string EquipId(string equipmentID) => "equip:" + equipmentID;

    /// <summary>Small red "NEW" dot, absolutely positioned top-right of its parent. Shared by the
    /// research and equipment popups so the badge looks identical everywhere.</summary>
    public static VisualElement CreateBadgeDot()
    {
        var dot = new VisualElement { name = "new-dot" };
        dot.pickingMode = PickingMode.Ignore;
        dot.style.position = Position.Absolute;
        dot.style.top = 6;
        dot.style.right = 6;
        dot.style.width = 16;
        dot.style.height = 16;
        dot.style.backgroundColor = new Color(0.92f, 0.22f, 0.22f);
        dot.style.borderTopLeftRadius = 8;
        dot.style.borderTopRightRadius = 8;
        dot.style.borderBottomLeftRadius = 8;
        dot.style.borderBottomRightRadius = 8;
        dot.style.borderTopWidth = 2;
        dot.style.borderBottomWidth = 2;
        dot.style.borderLeftWidth = 2;
        dot.style.borderRightWidth = 2;
        var b = new Color(1f, 1f, 1f, 0.9f);
        dot.style.borderTopColor = b;
        dot.style.borderBottomColor = b;
        dot.style.borderLeftColor = b;
        dot.style.borderRightColor = b;
        return dot;
    }

    /// <summary>Procedural filled circle sprite (white, tint at the renderer) for uGUI/world badge dots.</summary>
    public static Sprite CreateCircleSprite(int sizePx = 48)
    {
        var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = sizePx / 2f;
        float radius = c - 0.5f;
        var pixels = new Color[sizePx * sizePx];
        for (int y = 0; y < sizePx; y++)
            for (int x = 0; x < sizePx; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                pixels[y * sizePx + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - dist + 1f));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), sizePx);
    }

    public void MarkSeen(string contentId)
    {
        if (string.IsNullOrEmpty(contentId)) return;
        if (seen.Add(contentId))
        {
            SaveManager.Instance?.SaveGame();
            OnChanged?.Invoke();
        }
    }

    // ── Availability ─────────────────────────────────

    private void EnsureSeeded()
    {
        if (seeded) return;
        foreach (string id in ComputeAvailable()) seen.Add(id);
        seeded = true;
    }

    private IEnumerable<string> ComputeAvailable()
    {
        if (ResearchManager.Instance != null)
        {
            foreach (var rd in ResearchManager.Instance.AllResearches())
            {
                if (rd == null || string.IsNullOrEmpty(rd.researchID)) continue;
                if (ResearchManager.Instance.IsResearchVisible(rd.researchID))
                    yield return ResearchId(rd.researchID);
            }
        }

        if (equipmentRegistry != null && equipmentRegistry.equipment != null)
        {
            foreach (EquipmentData eq in equipmentRegistry.equipment)
            {
                if (eq == null || string.IsNullOrEmpty(eq.equipmentID)) continue;
                if (!string.IsNullOrEmpty(eq.requiredFeatureFlag))
                {
                    if (ResearchManager.Instance == null ||
                        !ResearchManager.Instance.IsFeatureUnlocked(eq.requiredFeatureFlag)) continue;
                }
                if (eq.IsUnlocked()) yield return EquipId(eq.equipmentID);
            }
        }
    }

    /// <summary>Recompute the available set; if new entries appeared since last time and
    /// <paramref name="announce"/> is true, fire an unlock toast naming them.</summary>
    private void Recompute(bool announce)
    {
        var current = new HashSet<string>(ComputeAvailable());

        if (announce)
        {
            var appeared = current.Where(id => !available.Contains(id)).ToList();
            if (appeared.Count > 0) AnnounceUnlock(appeared);
        }

        available.Clear();
        foreach (string id in current) available.Add(id);
        OnChanged?.Invoke();
    }

    private void AnnounceUnlock(List<string> appeared)
    {
        int newResearch = appeared.Count(id => id.StartsWith("research:"));
        var newEquipNames = appeared
            .Where(id => id.StartsWith("equip:"))
            .Select(id => EquipDisplayName(id.Substring("equip:".Length)))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        string headline = newEquipNames.Count > 0
            ? $"\U0001F513 {newEquipNames[0]} Unlocked!"
            : "\U0001F513 New Research Available";

        var parts = new List<string>();
        if (newEquipNames.Count > 0)
            parts.Add(newEquipNames.Count == 1 ? "New upgrades" : $"{newEquipNames.Count} new equipment");
        if (newResearch > 0)
            parts.Add(newResearch == 1 ? "1 new research" : $"{newResearch} new researches");

        ToastManager.Show(headline, string.Join("  +  ", parts), ToastManager.ToastKind.Unlock);
    }

    private string EquipDisplayName(string equipmentID)
    {
        if (equipmentRegistry == null || equipmentRegistry.equipment == null) return equipmentID;
        var eq = equipmentRegistry.equipment.FirstOrDefault(e => e != null && e.equipmentID == equipmentID);
        return eq != null && !string.IsNullOrEmpty(eq.displayName) ? eq.displayName : equipmentID;
    }

    // ── Event wiring ─────────────────────────────────

    private void Subscribe()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradePurchased += OnAvailabilityMaybeChanged;
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnFeatureFlagUnlocked += OnAvailabilityMaybeChanged;
            ResearchManager.Instance.OnResearchLeveledUp += OnResearchLeveledUp;
        }
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.OnAnimalUnlocked += OnAvailabilityMaybeChanged;
    }

    private void Unsubscribe()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradePurchased -= OnAvailabilityMaybeChanged;
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnFeatureFlagUnlocked -= OnAvailabilityMaybeChanged;
            ResearchManager.Instance.OnResearchLeveledUp -= OnResearchLeveledUp;
        }
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.OnAnimalUnlocked -= OnAvailabilityMaybeChanged;
    }

    private void OnAvailabilityMaybeChanged(string _) => Recompute(announce: true);
    private void OnResearchLeveledUp(string _, int __) => Recompute(announce: true);
}
