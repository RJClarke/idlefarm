using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SeedSelectionPopup : MonoBehaviour
{
    public static SeedSelectionPopup Instance { get; private set; }

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset fieldTileTemplate;
    [SerializeField] private VisualTreeAsset seedTileTemplate;
    [SerializeField] private VisualTreeAsset equipmentTileTemplate;

    [Header("Data")]
    [SerializeField] private CropDatabase cropDatabase;
    [Tooltip("All equipment shown in the bottom rail. Locked items render disabled.")]
    [SerializeField] private EquipmentData[] availableEquipment;

    // ── Events kept identical to the old API ────────────────────
    public event Action OnSelectionSaved;
    public event Action OnCancelled;

    // ── Persistent state (PlayerPrefs) ──────────────────────────
    private SeedSelectionData selectionData;
    private Dictionary<int, int> zoneEquipmentIndex = new Dictionary<int, int>();
    private const string EQUIP_PREFS_KEY = "EquipmentSelectionData";

    // ── UI Toolkit refs ─────────────────────────────────────────
    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement popupFrame;
    private VisualElement backdrop;
    private Button closeButton;
    private Button cancelButton;
    private Button saveButton;
    private ScrollView seedRail;
    private ScrollView equipmentRail;

    private readonly Dictionary<int, VisualElement> fieldTiles = new Dictionary<int, VisualElement>();
    private readonly List<VisualElement> seedRailTiles = new List<VisualElement>();
    private readonly List<VisualElement> equipmentRailTiles = new List<VisualElement>();

    private bool isOpen;
    private int selectedZoneID = -1;

    // ────────────────────────────────────────────────────────────
    // Lifecycle
    // ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        CacheElements();
        if (root != null) WireCallbacks();

        // Start hidden — Show() must be explicitly invoked by the user.
        isOpen = false;
        if (popupRoot != null)
        {
            popupRoot.RemoveFromClassList("open");
            popupRoot.style.display = DisplayStyle.None;
        }
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void CacheElements()
    {
        if (document == null) return;
        root = document.rootVisualElement;
        if (root == null) return;

        root.pickingMode = PickingMode.Ignore;

        popupRoot     = root.Q<VisualElement>("popup-root");
        popupFrame    = root.Q<VisualElement>("popup-frame");
        backdrop      = root.Q<VisualElement>("backdrop");
        closeButton   = root.Q<Button>("close-button");
        cancelButton  = root.Q<Button>("cancel-button");
        saveButton    = root.Q<Button>("save-button");
        seedRail      = root.Q<ScrollView>("seed-rail");
        equipmentRail = root.Q<ScrollView>("equipment-rail");

        fieldTiles.Clear();
        fieldTiles[1] = root.Q<VisualElement>("field-1");
        fieldTiles[2] = root.Q<VisualElement>("field-2");
        fieldTiles[3] = root.Q<VisualElement>("field-3");
        fieldTiles[4] = root.Q<VisualElement>("field-4");
    }

    private void WireCallbacks()
    {
        if (closeButton  != null) closeButton.clicked  += OnCancelClicked;
        if (cancelButton != null) cancelButton.clicked += OnCancelClicked;
        if (saveButton   != null) saveButton.clicked   += OnSaveClicked;
        if (backdrop     != null) backdrop.RegisterCallback<ClickEvent>(_ => OnCancelClicked());
    }

    // ────────────────────────────────────────────────────────────
    // Public API (kept identical to old script)
    // ────────────────────────────────────────────────────────────

    public void Show()
    {
        if (isOpen) return;
        isOpen = true;
        selectedZoneID = -1;

        selectionData = SeedSelectionData.Load();
        LoadEquipmentAssignments();
        ScrubLockedZoneAssignments();

        BuildFieldTiles();
        BuildSeedRail();
        BuildEquipmentRail();

        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
    }

    public void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot != null)
        {
            popupRoot.RemoveFromClassList("open");
            popupRoot.schedule.Execute(() =>
            {
                if (isOpen) return;
                popupRoot.style.display = DisplayStyle.None;
                if (root != null) root.pickingMode = PickingMode.Ignore;
            }).StartingIn(260);
        }
    }

    public bool IsReadyToRun()
    {
        if (selectionData == null) selectionData = SeedSelectionData.Load();
        return selectionData.AreAllUnlockedZonesFilled();
    }

    public Dictionary<int, CropData> LoadAndApplySavedSelections()
    {
        selectionData = SeedSelectionData.Load();
        LoadEquipmentAssignments();
        ApplyEquipmentToManager();
        return selectionData.ToZoneSeedDictionary(cropDatabase);
    }

    // ────────────────────────────────────────────────────────────
    // Field tile build / refresh
    // ────────────────────────────────────────────────────────────

    private void BuildFieldTiles()
    {
        if (fieldTileTemplate == null) { Debug.LogWarning("[SeedSelectionPopup] fieldTileTemplate not assigned"); return; }

        for (int zone = 1; zone <= 4; zone++)
        {
            VisualElement container = fieldTiles[zone];
            if (container == null) continue;

            container.Clear();
            TemplateContainer instance = fieldTileTemplate.Instantiate();
            container.Add(instance);

            int capturedZone = zone;
            container.RegisterCallback<ClickEvent>(_ => OnFieldClicked(capturedZone));

            Button cropClear = instance.Q<Button>("crop-clear");
            Button equipClear = instance.Q<Button>("equip-clear");
            if (cropClear != null) cropClear.clicked += () => OnClearCrop(capturedZone);
            if (equipClear != null) equipClear.clicked += () => OnClearEquipment(capturedZone);

            RefreshFieldTile(zone);
        }
    }

    private void RefreshFieldTile(int zone)
    {
        VisualElement container = fieldTiles[zone];
        if (container == null) return;

        bool unlocked = selectionData.IsZoneUnlocked(zone);
        bool selected = (zone == selectedZoneID);

        container.RemoveFromClassList("field-tile--selected");
        container.RemoveFromClassList("field-tile--locked");
        if (!unlocked) container.AddToClassList("field-tile--locked");
        else if (selected) container.AddToClassList("field-tile--selected");

        Label fieldLabel = container.Q<Label>("field-label");
        if (fieldLabel != null) fieldLabel.text = "Field " + zone;

        // Crop slot
        VisualElement cropSlot   = container.Q<VisualElement>("crop-slot");
        VisualElement cropImage  = container.Q<VisualElement>("crop-image");
        Label         cropName   = container.Q<Label>("crop-name");
        string assignedCropName = selectionData.GetCropName(zone);
        CropData assignedCrop = (assignedCropName != null && cropDatabase != null)
            ? cropDatabase.GetCropByName(assignedCropName) : null;

        if (cropSlot != null)
        {
            cropSlot.RemoveFromClassList("slot--filled");
            if (assignedCrop != null)
            {
                cropSlot.AddToClassList("slot--filled");
                if (cropImage != null && assignedCrop.seedPacketSprite != null)
                    cropImage.style.backgroundImage = new StyleBackground(assignedCrop.seedPacketSprite);
                else if (cropImage != null && assignedCrop.cropSprite != null)
                    cropImage.style.backgroundImage = new StyleBackground(assignedCrop.cropSprite);
                if (cropName != null) cropName.text = assignedCrop.cropName;
            }
            else
            {
                if (cropImage != null) cropImage.style.backgroundImage = StyleKeyword.None;
            }
        }

        // Equipment slot
        VisualElement equipSlot  = container.Q<VisualElement>("equip-slot");
        VisualElement equipImage = container.Q<VisualElement>("equip-image");
        Label         equipName  = container.Q<Label>("equip-name");
        EquipmentData assignedEquip = GetAssignedEquipment(zone);

        if (equipSlot != null)
        {
            equipSlot.RemoveFromClassList("slot--filled");
            if (assignedEquip != null)
            {
                equipSlot.AddToClassList("slot--filled");
                if (equipImage != null)
                {
                    if (assignedEquip.iconSprite != null)
                        equipImage.style.backgroundImage = new StyleBackground(assignedEquip.iconSprite);
                    else
                        equipImage.style.backgroundImage = StyleKeyword.None;
                }
                if (equipName != null) equipName.text = assignedEquip.displayName;
            }
            else
            {
                if (equipImage != null) equipImage.style.backgroundImage = StyleKeyword.None;
            }
        }
    }

    private void RefreshAllFieldTiles()
    {
        for (int z = 1; z <= 4; z++) RefreshFieldTile(z);
    }

    // ────────────────────────────────────────────────────────────
    // Seed rail
    // ────────────────────────────────────────────────────────────

    private void BuildSeedRail()
    {
        if (seedRail == null || seedTileTemplate == null || cropDatabase == null) return;

        foreach (VisualElement old in seedRailTiles) old.RemoveFromHierarchy();
        seedRailTiles.Clear();

        foreach (CropData crop in cropDatabase.allCrops)
        {
            if (crop == null) continue;
            TemplateContainer tile = seedTileTemplate.Instantiate();
            VisualElement tileRoot = tile.Q(className: "rail-tile") ?? tile.contentContainer;
            VisualElement img = tile.Q<VisualElement>("tile-image");
            Label label = tile.Q<Label>("tile-label");
            Button btn = tile.Q<Button>() ?? tileRoot as Button;

            if (img != null && crop.seedPacketSprite != null)
                img.style.backgroundImage = new StyleBackground(crop.seedPacketSprite);
            else if (img != null && crop.cropSprite != null)
                img.style.backgroundImage = new StyleBackground(crop.cropSprite);
            if (label != null) label.text = crop.cropName;

            CropData captured = crop;
            if (btn != null) btn.clicked += () => OnSeedTileClicked(captured);

            seedRail.Add(tile);
            seedRailTiles.Add(tile);
        }

        RefreshSeedRailStates();
    }

    private void RefreshSeedRailStates()
    {
        if (cropDatabase == null) return;
        int i = 0;
        foreach (CropData crop in cropDatabase.allCrops)
        {
            if (crop == null) continue;
            if (i >= seedRailTiles.Count) break;
            VisualElement tile = seedRailTiles[i++];
            VisualElement tileRoot = tile.Q(className: "rail-tile") ?? tile;
            tileRoot.RemoveFromClassList("rail-tile--assigned");
            tileRoot.RemoveFromClassList("rail-tile--locked");
            if (selectionData.IsCropAssigned(crop.cropName))
                tileRoot.AddToClassList("rail-tile--assigned");
        }
    }

    // ────────────────────────────────────────────────────────────
    // Equipment rail
    // ────────────────────────────────────────────────────────────

    private void BuildEquipmentRail()
    {
        if (equipmentRail == null || equipmentTileTemplate == null || availableEquipment == null) return;

        foreach (VisualElement old in equipmentRailTiles) old.RemoveFromHierarchy();
        equipmentRailTiles.Clear();

        foreach (EquipmentData eq in availableEquipment)
        {
            if (eq == null) continue;
            TemplateContainer tile = equipmentTileTemplate.Instantiate();
            VisualElement tileRoot = tile.Q(className: "rail-tile") ?? tile.contentContainer;
            VisualElement img = tile.Q<VisualElement>("tile-image");
            Label label = tile.Q<Label>("tile-label");
            Button btn = tile.Q<Button>() ?? tileRoot as Button;

            if (img != null)
            {
                if (eq.iconSprite != null)
                    img.style.backgroundImage = new StyleBackground(eq.iconSprite);
                else
                    img.style.backgroundImage = StyleKeyword.None;
            }
            if (label != null) label.text = eq.displayName;

            EquipmentData captured = eq;
            if (btn != null) btn.clicked += () => OnEquipmentTileClicked(captured);

            equipmentRail.Add(tile);
            equipmentRailTiles.Add(tile);
        }

        RefreshEquipmentRailStates();
    }

    private void RefreshEquipmentRailStates()
    {
        if (availableEquipment == null) return;
        for (int i = 0; i < equipmentRailTiles.Count && i < availableEquipment.Length; i++)
        {
            EquipmentData eq = availableEquipment[i];
            if (eq == null) continue;

            VisualElement tile = equipmentRailTiles[i];
            VisualElement tileRoot = tile.Q(className: "rail-tile") ?? tile;
            Button btn = tile.Q<Button>() ?? tileRoot as Button;

            tileRoot.RemoveFromClassList("rail-tile--locked");
            tileRoot.RemoveFromClassList("rail-tile--assigned");

            bool unlocked = eq.IsUnlocked();
            if (!unlocked) tileRoot.AddToClassList("rail-tile--locked");
            if (btn != null) btn.SetEnabled(unlocked);
        }
    }

    // ────────────────────────────────────────────────────────────
    // Interaction handlers
    // ────────────────────────────────────────────────────────────

    private void OnFieldClicked(int zone)
    {
        if (!selectionData.IsZoneUnlocked(zone)) return;
        selectedZoneID = (selectedZoneID == zone) ? -1 : zone;
        RefreshAllFieldTiles();
    }

    private void OnSeedTileClicked(CropData crop)
    {
        if (crop == null) return;
        if (selectionData.IsCropAssigned(crop.cropName)) return;

        int targetZone = selectedZoneID > 0 ? selectedZoneID : selectionData.GetFirstEmptyZone();
        if (targetZone <= 0 || !selectionData.IsZoneUnlocked(targetZone)) return;

        selectionData.AssignCrop(targetZone, crop);
        selectedZoneID = -1;
        RefreshAllFieldTiles();
        RefreshSeedRailStates();
    }

    private void OnEquipmentTileClicked(EquipmentData eq)
    {
        if (eq == null || !eq.IsUnlocked()) return;

        int targetZone = selectedZoneID > 0 ? selectedZoneID : GetFirstZoneWithoutEquipment();
        if (targetZone <= 0 || !selectionData.IsZoneUnlocked(targetZone)) return;

        int idx = Array.IndexOf(availableEquipment, eq);
        if (idx < 0) return;

        zoneEquipmentIndex[targetZone] = idx;
        selectedZoneID = -1;
        RefreshAllFieldTiles();
    }

    private void OnClearCrop(int zone)
    {
        selectionData.ClearZone(zone);
        RefreshFieldTile(zone);
        RefreshSeedRailStates();
    }

    private void OnClearEquipment(int zone)
    {
        zoneEquipmentIndex.Remove(zone);
        RefreshFieldTile(zone);
    }

    private void OnCancelClicked()
    {
        Hide();
        OnCancelled?.Invoke();
    }

    private void OnSaveClicked()
    {
        if (selectionData == null) selectionData = SeedSelectionData.Load();
        selectionData.Save();
        SaveEquipmentAssignments();
        ApplyEquipmentToManager();
        OnSelectionSaved?.Invoke();
        Hide();
        Debug.Log("Field configuration saved");
    }

    // ────────────────────────────────────────────────────────────
    // Equipment persistence (identical format to old script)
    // ────────────────────────────────────────────────────────────

    private EquipmentData GetAssignedEquipment(int zone)
    {
        if (!zoneEquipmentIndex.TryGetValue(zone, out int idx)) return null;
        if (availableEquipment == null || idx < 0 || idx >= availableEquipment.Length) return null;
        return availableEquipment[idx];
    }

    private int GetFirstZoneWithoutEquipment()
    {
        for (int z = 1; z <= 4; z++)
        {
            if (selectionData.IsZoneUnlocked(z) && !zoneEquipmentIndex.ContainsKey(z))
                return z;
        }
        return -1;
    }

    private void ScrubLockedZoneAssignments()
    {
        List<int> toRemove = new List<int>();
        foreach (int z in zoneEquipmentIndex.Keys)
            if (!selectionData.IsZoneUnlocked(z)) toRemove.Add(z);
        foreach (int z in toRemove) zoneEquipmentIndex.Remove(z);

        // Locked zones must never carry a crop assignment either.
        for (int z = 1; z <= 4; z++)
            if (!selectionData.IsZoneUnlocked(z))
                selectionData.ClearZone(z);
    }

    private void ApplyEquipmentToManager()
    {
        if (EquipmentManager.Instance == null) return;
        EquipmentManager.Instance.ClearAllAssignments();
        foreach (var kvp in zoneEquipmentIndex)
        {
            int idx = kvp.Value;
            if (idx >= 0 && availableEquipment != null && idx < availableEquipment.Length)
                EquipmentManager.Instance.AssignEquipment(kvp.Key, availableEquipment[idx]);
        }
    }

    private void SaveEquipmentAssignments()
    {
        List<string> entries = new List<string>();
        foreach (var kvp in zoneEquipmentIndex)
        {
            int idx = kvp.Value;
            if (idx >= 0 && availableEquipment != null && idx < availableEquipment.Length)
            {
                EquipmentData eq = availableEquipment[idx];
                if (eq != null) entries.Add($"{kvp.Key}:{eq.equipmentID}");
            }
        }
        PlayerPrefs.SetString(EQUIP_PREFS_KEY, string.Join(";", entries));
        PlayerPrefs.Save();
    }

    private void LoadEquipmentAssignments()
    {
        zoneEquipmentIndex.Clear();
        if (!PlayerPrefs.HasKey(EQUIP_PREFS_KEY)) return;
        if (availableEquipment == null || availableEquipment.Length == 0) return;

        string saved = PlayerPrefs.GetString(EQUIP_PREFS_KEY);
        if (string.IsNullOrEmpty(saved)) return;

        foreach (string entry in saved.Split(';'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], out int zoneId)) continue;

            string equipId = parts[1];
            int idx = Array.FindIndex(availableEquipment, e => e != null && e.equipmentID == equipId);
            if (idx >= 0 && availableEquipment[idx].IsUnlocked())
                zoneEquipmentIndex[zoneId] = idx;
        }
    }
}
