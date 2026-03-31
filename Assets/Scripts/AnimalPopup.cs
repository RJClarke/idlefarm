using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalPopup : MonoBehaviour
{
    public static AnimalPopup Instance { get; private set; }

    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private GameObject backdrop;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI gemCountText;

    [Header("Animal List")]
    [SerializeField] private Transform animalListContainer;
    [SerializeField] private GameObject animalRowPrefab;

    private List<GameObject> rowInstances = new List<GameObject>();

    private System.Action<AnimalData> onEquipped;
    private System.Action onUnequipped;
    private System.Action<string> onUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void Start()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemsChanged += UpdateGemCount;

        if (AnimalManager.Instance != null)
        {
            onEquipped = (_) => RefreshList();
            onUnequipped = RefreshList;
            onUnlocked = (_) => RefreshList();
            AnimalManager.Instance.OnAnimalEquipped += onEquipped;
            AnimalManager.Instance.OnAnimalUnequipped += onUnequipped;
            AnimalManager.Instance.OnAnimalUnlocked += onUnlocked;
        }
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemsChanged -= UpdateGemCount;

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped -= onEquipped;
            AnimalManager.Instance.OnAnimalUnequipped -= onUnequipped;
            AnimalManager.Instance.OnAnimalUnlocked -= onUnlocked;
        }
    }

    // ── Show / Hide ──────────────────────────────

    public void Show()
    {
        popupRoot.SetActive(true);

        UpdateGemCount(CurrencyManager.Instance.Gems);
        RefreshList();

        // Animate in (scale + fade)
        popupPanel.localScale = Vector3.one * 0.8f;
        popupCanvasGroup.alpha = 0f;

        LeanTween.scale(popupPanel, Vector3.one, 0.3f).setEaseOutQuad();
        LeanTween.alphaCanvas(popupCanvasGroup, 1f, 0.3f).setEaseOutQuad();
    }

    public void Hide()
    {
        LeanTween.scale(popupPanel, Vector3.one * 0.8f, 0.2f).setEaseInQuad();
        LeanTween.alphaCanvas(popupCanvasGroup, 0f, 0.2f).setEaseInQuad().setOnComplete(() =>
        {
            popupRoot.SetActive(false);
        });
    }

    public void OnBackdropClick()
    {
        Hide();
    }

    public void OnCloseClick()
    {
        Hide();
    }

    // ── List Population ──────────────────────────────

    private void RefreshList()
    {
        // Clear existing rows
        foreach (GameObject row in rowInstances)
        {
            Destroy(row);
        }
        rowInstances.Clear();

        List<AnimalData> animals = AnimalManager.Instance.GetAllAnimals();

        foreach (AnimalData animal in animals)
        {
            GameObject row = Instantiate(animalRowPrefab, animalListContainer);
            rowInstances.Add(row);
            SetupRow(row, animal);
        }
    }

    private void SetupRow(GameObject row, AnimalData animal)
    {
        // Find row child elements by name
        TextMeshProUGUI nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI descText = row.transform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI emojiText = row.transform.Find("EmojiText")?.GetComponent<TextMeshProUGUI>();
        Button actionButton = row.transform.Find("ActionButton")?.GetComponent<Button>();
        TextMeshProUGUI actionText = actionButton?.GetComponentInChildren<TextMeshProUGUI>();
        Image rowBackground = row.GetComponent<Image>();

        bool isUnlocked = AnimalManager.Instance.IsUnlocked(animal.animalID);
        bool isEquipped = AnimalManager.Instance.GetEquippedAnimalID() == animal.animalID;

        // Set text
        if (nameText != null) nameText.text = animal.displayName;
        if (descText != null) descText.text = animal.description;
        if (emojiText != null) emojiText.text = animal.animalEmoji;

        // Set state
        if (isEquipped)
        {
            // Green highlight — equipped
            if (rowBackground != null) rowBackground.color = new Color(0.29f, 0.49f, 0.18f, 0.3f);
            if (actionText != null) actionText.text = "EQUIPPED";
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() =>
                {
                    AnimalManager.Instance.UnequipAnimal();
                });
            }
        }
        else if (isUnlocked)
        {
            // Neutral — unlocked, tap to equip
            if (rowBackground != null) rowBackground.color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
            if (actionText != null) actionText.text = "EQUIP";
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                string id = animal.animalID;
                actionButton.onClick.AddListener(() =>
                {
                    AnimalManager.Instance.EquipAnimal(id);
                });
            }
        }
        else
        {
            // Locked
            bool canAfford = CurrencyManager.Instance.CanAffordGems(animal.gemCost);

            if (rowBackground != null) rowBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.15f);
            if (emojiText != null) emojiText.alpha = 0.4f;
            if (nameText != null) nameText.color = new Color(0.6f, 0.6f, 0.6f);
            if (descText != null) descText.color = new Color(0.5f, 0.5f, 0.5f);

            string costDisplay = $"💎 {animal.gemCost:N0}";
            if (actionText != null) actionText.text = costDisplay;

            if (actionButton != null)
            {
                actionButton.interactable = canAfford;
                actionButton.onClick.RemoveAllListeners();

                if (canAfford)
                {
                    string id = animal.animalID;
                    int cost = animal.gemCost;
                    string name = animal.displayName;
                    actionButton.onClick.AddListener(() =>
                    {
                        if (AnimalManager.Instance.TryUnlockAnimal(id))
                        {
                            Debug.Log($"Unlocked {name}!");
                        }
                    });
                }
            }
        }
    }

    private void UpdateGemCount(int gems)
    {
        if (gemCountText != null)
            gemCountText.text = $"💎 {gems:N0}";
    }
}
