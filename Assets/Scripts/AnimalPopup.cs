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
    [SerializeField] private GameObject animalRowPrefab; // kept for reference but not used
    [SerializeField] private Sprite rowBackgroundSprite;
    [SerializeField] private Sprite actionButtonSprite;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;

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

        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClick);
        if (backdropButton != null) backdropButton.onClick.AddListener(OnBackdropClick);
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

    public void OnBackdropClick() => Hide();
    public void OnCloseClick() => Hide();

    // ── List Population (code-built rows) ──────────────────────────────

    private void RefreshList()
    {
        foreach (GameObject row in rowInstances)
            Destroy(row);
        rowInstances.Clear();

        List<AnimalData> animals = AnimalManager.Instance.GetAllAnimals();

        foreach (AnimalData animal in animals)
        {
            GameObject row = CreateRow(animal);
            row.transform.SetParent(animalListContainer, false);
            rowInstances.Add(row);
        }
    }

    private GameObject CreateRow(AnimalData animal)
    {
        bool isUnlocked = AnimalManager.Instance.IsUnlocked(animal.animalID);
        bool isEquipped = AnimalManager.Instance.GetEquippedAnimalID() == animal.animalID;

        // Row container
        GameObject row = new GameObject(animal.displayName + "Row");
        RectTransform rowRT = row.AddComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, 130);

        Image rowBg = row.AddComponent<Image>();
        if (rowBackgroundSprite != null)
        {
            rowBg.sprite = rowBackgroundSprite;
            rowBg.type = Image.Type.Sliced;
        }
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 130;
        rowLE.preferredHeight = 130;

        HorizontalLayoutGroup rowHLG = row.AddComponent<HorizontalLayoutGroup>();
        rowHLG.spacing = 15;
        rowHLG.padding = new RectOffset(15, 15, 10, 10);
        rowHLG.childAlignment = TextAnchor.MiddleLeft;
        rowHLG.childForceExpandWidth = false;
        rowHLG.childForceExpandHeight = true;
        rowHLG.childControlWidth = true;
        rowHLG.childControlHeight = true;

        // Emoji
        GameObject emojiGO = new GameObject("Emoji");
        emojiGO.transform.SetParent(row.transform, false);
        TextMeshProUGUI emojiTMP = emojiGO.AddComponent<TextMeshProUGUI>();
        emojiTMP.text = animal.animalEmoji;
        emojiTMP.fontSize = 48;
        emojiTMP.alignment = TextAlignmentOptions.Center;
        LayoutElement emojiLE = emojiGO.AddComponent<LayoutElement>();
        emojiLE.preferredWidth = 80;
        emojiLE.minWidth = 80;

        // Text group (name + description)
        GameObject textGroup = new GameObject("TextGroup");
        textGroup.transform.SetParent(row.transform, false);
        RectTransform tgRT = textGroup.AddComponent<RectTransform>();
        VerticalLayoutGroup tgVLG = textGroup.AddComponent<VerticalLayoutGroup>();
        tgVLG.spacing = 2;
        tgVLG.childAlignment = TextAnchor.MiddleLeft;
        tgVLG.childForceExpandWidth = true;
        tgVLG.childForceExpandHeight = false;
        tgVLG.childControlWidth = true;
        tgVLG.childControlHeight = true;
        LayoutElement tgLE = textGroup.AddComponent<LayoutElement>();
        tgLE.flexibleWidth = 1;

        // Name text
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(textGroup.transform, false);
        TextMeshProUGUI nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = animal.displayName;
        nameTMP.fontSize = 28;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = new Color(0.96f, 0.87f, 0.7f, 1f);
        LayoutElement nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 35;

        // Description text
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(textGroup.transform, false);
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = animal.description;
        descTMP.fontSize = 18;
        descTMP.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        descTMP.enableWordWrapping = true;
        LayoutElement descLE = descGO.AddComponent<LayoutElement>();
        descLE.preferredHeight = 50;
        descLE.flexibleHeight = 1;

        // Action button
        GameObject btnGO = new GameObject("ActionBtn");
        btnGO.transform.SetParent(row.transform, false);
        RectTransform btnRT = btnGO.AddComponent<RectTransform>();
        Image btnImg = btnGO.AddComponent<Image>();
        if (actionButtonSprite != null)
        {
            btnImg.sprite = actionButtonSprite;
            btnImg.type = Image.Type.Sliced;
        }
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 160;
        btnLE.minWidth = 160;

        // Action button text
        GameObject btnTextGO = new GameObject("BtnText");
        btnTextGO.transform.SetParent(btnGO.transform, false);
        RectTransform btnTextRT = btnTextGO.AddComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero;
        btnTextRT.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnTMP.fontSize = 22;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;

        // Apply state
        if (isEquipped)
        {
            // Bright green tint — clearly active
            rowBg.color = new Color(0.4f, 0.7f, 0.3f, 0.9f);
            btnImg.color = new Color(0.2f, 0.4f, 0.15f, 0.9f);
            btnTMP.text = "EQUIPPED";
            btnTMP.color = new Color(0.8f, 1f, 0.7f, 1f);
            nameTMP.color = Color.white;
            descTMP.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.onClick.AddListener(() => {
                AnimalManager.Instance.UnequipAnimal();
                RefreshList();
            });
        }
        else if (isUnlocked)
        {
            // Warm wood tint — available
            rowBg.color = new Color(0.85f, 0.75f, 0.6f, 0.9f);
            btnImg.color = new Color(0.3f, 0.55f, 0.25f, 0.9f);
            btnTMP.text = "EQUIP";
            btnTMP.color = Color.white;
            nameTMP.color = new Color(0.25f, 0.2f, 0.12f, 1f);
            descTMP.color = new Color(0.4f, 0.35f, 0.25f, 1f);
            string id = animal.animalID;
            btn.onClick.AddListener(() => {
                AnimalManager.Instance.EquipAnimal(id);
                RefreshList();
            });
        }
        else
        {
            // Locked
            bool canAfford = CurrencyManager.Instance.CanAffordGems(animal.gemCost);

            if (canAfford)
            {
                // Can afford — highlighted, inviting
                rowBg.color = new Color(0.7f, 0.65f, 0.55f, 0.85f);
                nameTMP.color = new Color(0.3f, 0.25f, 0.15f, 1f);
                descTMP.color = new Color(0.45f, 0.4f, 0.3f, 1f);
                btnImg.color = new Color(0.55f, 0.3f, 0.7f, 0.9f);
                btnTMP.text = $"{animal.gemCost:N0}";
                btnTMP.color = Color.white;
                string id = animal.animalID;
                btn.onClick.AddListener(() => {
                    if (AnimalManager.Instance.TryUnlockAnimal(id))
                        RefreshList();
                });
            }
            else
            {
                // Can't afford — dimmed, locked feel
                rowBg.color = new Color(0.45f, 0.4f, 0.35f, 0.5f);
                emojiTMP.alpha = 0.35f;
                nameTMP.color = new Color(0.55f, 0.5f, 0.45f, 1f);
                descTMP.color = new Color(0.45f, 0.4f, 0.35f, 1f);
                btnImg.color = new Color(0.35f, 0.3f, 0.25f, 0.6f);
                btnTMP.text = $"{animal.gemCost:N0}";
                btnTMP.color = new Color(0.55f, 0.5f, 0.45f, 1f);
                btn.interactable = false;
            }
        }

        return row;
    }

    private void UpdateGemCount(int gems)
    {
        if (gemCountText != null)
            gemCountText.text = $"{gems:N0}";
    }
}
