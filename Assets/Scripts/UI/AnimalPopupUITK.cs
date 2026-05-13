using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AnimalPopupUITK : MonoBehaviour
{
    public static AnimalPopupUITK Instance { get; private set; }

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset rowTemplate;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement popupFrame;
    private VisualElement backdrop;
    private Button closeButton;
    private Label gemCountLabel;
    private ScrollView animalList;

    private bool isOpen;
    private bool eventsSubscribed;
    private readonly List<VisualElement> spawnedRows = new List<VisualElement>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        CacheElements();
        WireCallbacks();
        TrySubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[AnimalPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot     = root.Q<VisualElement>("popup-root");
        popupFrame    = root.Q<VisualElement>("popup-frame");
        backdrop      = root.Q<VisualElement>("backdrop");
        closeButton   = root.Q<Button>("close-button");
        gemCountLabel = root.Q<Label>("gem-count");
        animalList    = root.Q<ScrollView>("animal-list");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.clicked += Close;
        if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        if (AnimalManager.Instance == null || CurrencyManager.Instance == null) return;

        AnimalManager.Instance.OnAnimalEquipped   += OnAnimalEquipped;
        AnimalManager.Instance.OnAnimalUnequipped += RefreshList;
        AnimalManager.Instance.OnAnimalUnlocked   += OnAnimalUnlocked;
        CurrencyManager.Instance.OnGemsChanged    += OnGemsChanged;
        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped   -= OnAnimalEquipped;
            AnimalManager.Instance.OnAnimalUnequipped -= RefreshList;
            AnimalManager.Instance.OnAnimalUnlocked   -= OnAnimalUnlocked;
        }
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemsChanged -= OnGemsChanged;
        eventsSubscribed = false;
    }

    private void OnAnimalEquipped(AnimalData _) => RefreshList();
    private void OnAnimalUnlocked(string _) => RefreshList();
    private void OnGemsChanged(int gems) { UpdateGemCount(); if (isOpen) RefreshList(); }

    // ── Open / Close ─────────────────────────────────────────────

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        UpdateGemCount();
        RefreshList();
    }

    public void Close()
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

    // ── Refresh ──────────────────────────────────────────────────

    private void UpdateGemCount()
    {
        if (gemCountLabel == null || CurrencyManager.Instance == null) return;
        gemCountLabel.text = CurrencyManager.Instance.Gems.ToString("N0");
    }

    private void RefreshList()
    {
        if (AnimalManager.Instance == null || animalList == null) return;

        foreach (VisualElement old in spawnedRows) old.RemoveFromHierarchy();
        spawnedRows.Clear();

        if (rowTemplate == null)
        {
            Debug.LogWarning("[AnimalPopupUITK] rowTemplate not assigned");
            return;
        }

        List<AnimalData> animals = AnimalManager.Instance.GetAllAnimals();
        foreach (AnimalData animal in animals)
        {
            TemplateContainer row = rowTemplate.Instantiate();
            BindRow(row, animal);
            animalList.Add(row);
            spawnedRows.Add(row);
        }
    }

    private void BindRow(TemplateContainer row, AnimalData animal)
    {
        VisualElement rowRoot       = row.Q(className: "animal-row") ?? row.contentContainer;
        VisualElement iconElem      = row.Q<VisualElement>("row-icon");
        Label         iconFallback  = row.Q<Label>("row-icon-fallback");
        Label         nameLabel     = row.Q<Label>("row-name");
        Label         descLabel     = row.Q<Label>("row-description");
        Button        actionButton  = row.Q<Button>("action-button");
        Label         actionLabel   = row.Q<Label>("action-label");
        Label         actionIcon    = row.Q<Label>("action-icon");

        if (nameLabel != null) nameLabel.text = animal.displayName;
        if (descLabel != null) descLabel.text = animal.description;

        if (iconElem != null)
        {
            if (animal.iconSprite != null)
            {
                iconElem.style.backgroundImage = new StyleBackground(animal.iconSprite);
                iconElem.style.display = DisplayStyle.Flex;
                if (iconFallback != null) iconFallback.style.display = DisplayStyle.None;
            }
            else
            {
                iconElem.style.display = DisplayStyle.None;
                if (iconFallback != null)
                {
                    iconFallback.text = string.IsNullOrEmpty(animal.animalEmoji) ? "?" : animal.animalEmoji;
                    iconFallback.style.display = DisplayStyle.Flex;
                }
            }
        }

        bool isUnlocked = AnimalManager.Instance.IsUnlocked(animal.animalID);
        bool isEquipped = AnimalManager.Instance.GetEquippedAnimalID() == animal.animalID;

        rowRoot.RemoveFromClassList("animal-row--equipped");
        rowRoot.RemoveFromClassList("animal-row--locked");
        rowRoot.RemoveFromClassList("animal-row--locked-dim");

        if (actionButton == null) return;

        actionButton.RemoveFromClassList("action-button--buy");
        actionButton.RemoveFromClassList("action-button--locked");
        actionButton.RemoveFromClassList("action-button--equip");
        actionButton.RemoveFromClassList("action-button--equipped");

        // Clear any previous click handler (rows are rebuilt, but TemplateContainer caches).
        actionButton.clickable = new Clickable(() => { });

        if (isEquipped)
        {
            rowRoot.AddToClassList("animal-row--equipped");
            actionButton.AddToClassList("action-button--equipped");
            if (actionLabel != null) actionLabel.text = "Unequip";
            if (actionIcon != null) actionIcon.style.display = DisplayStyle.None;
            actionButton.SetEnabled(true);
            actionButton.clickable = new Clickable(() => AnimalManager.Instance.UnequipAnimal());
        }
        else if (isUnlocked)
        {
            actionButton.AddToClassList("action-button--equip");
            if (actionLabel != null) actionLabel.text = "Equip";
            if (actionIcon != null) actionIcon.style.display = DisplayStyle.None;
            actionButton.SetEnabled(true);
            string capturedID = animal.animalID;
            actionButton.clickable = new Clickable(() => AnimalManager.Instance.EquipAnimal(capturedID));
        }
        else
        {
            bool canAfford = CurrencyManager.Instance != null &&
                             CurrencyManager.Instance.CanAffordGems(animal.gemCost);

            actionButton.AddToClassList(canAfford ? "action-button--buy" : "action-button--locked");
            if (!canAfford) rowRoot.AddToClassList("animal-row--locked-dim");

            if (actionLabel != null) actionLabel.text = animal.gemCost.ToString("N0");
            if (actionIcon != null)
            {
                actionIcon.text = "◆";
                actionIcon.style.display = DisplayStyle.Flex;
            }

            if (canAfford)
            {
                string capturedID = animal.animalID;
                actionButton.SetEnabled(true);
                actionButton.clickable = new Clickable(() => AnimalManager.Instance.TryUnlockAnimal(capturedID));
            }
            else
            {
                actionButton.SetEnabled(false);
            }
        }
    }
}
