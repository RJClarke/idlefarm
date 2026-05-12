using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class QuestPopup : MonoBehaviour
{
    public static QuestPopup Instance { get; private set; }

    [Header("Popup Structure")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupContainer;
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;

    [Header("Weekly Track Strip")]
    [SerializeField] private TextMeshProUGUI weeklyCountLabel;
    [SerializeField] private Slider weeklyProgressBar;
    [SerializeField] private List<MilestoneChip> milestoneChips;

    [Header("Quest List")]
    [SerializeField] private Transform questListContent;
    [SerializeField] private GameObject questRowPrefab;
    [SerializeField] private TextMeshProUGUI footerText;

    [Header("Quest Button")]
    [SerializeField] private Button questButton;
    [SerializeField] private GameObject notificationDot;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    private bool isOpen = false;
    private List<GameObject> spawnedRows = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        popupContainer.localScale = Vector3.one * 0.8f;

        ApplyLayoutFix();
    }

    private void ApplyLayoutFix()
    {
        VerticalLayoutGroup popupVLG = popupContainer.GetComponent<VerticalLayoutGroup>();
        if (popupVLG != null)
        {
            popupVLG.padding = new RectOffset(12, 12, 10, 8);
            popupVLG.spacing = 8;
        }

        VerticalLayoutGroup weeklyVLG = weeklyCountLabel?.transform?.parent?.GetComponent<VerticalLayoutGroup>();
        if (weeklyVLG != null)
        {
            weeklyVLG.padding = new RectOffset(10, 10, 8, 6);
            weeklyVLG.spacing = 6;
        }

        VerticalLayoutGroup contentVLG = questListContent?.GetComponent<VerticalLayoutGroup>();
        if (contentVLG != null)
        {
            contentVLG.padding = new RectOffset(6, 6, 6, 6);
            contentVLG.spacing = 8;
        }

        HorizontalLayoutGroup headerHLG = popupContainer.Find("Header")?.GetComponent<HorizontalLayoutGroup>();
        if (headerHLG != null)
        {
            headerHLG.padding = new RectOffset(14, 14, 0, 0);
        }

        SetupScrollbar();
    }

    private void SetupScrollbar()
    {
        Transform scrollViewTf = popupContainer.Find("ScrollView");
        if (scrollViewTf == null) return;

        // Find existing scrollbar or create one
        Transform existingBar = scrollViewTf.Find("ScrollbarVisual");
        if (existingBar != null) return;

        // Look for the ScrollRect
        ScrollRect scrollRect = scrollViewTf.GetComponent<ScrollRect>();
        if (scrollRect == null) return;

        // Create scrollbar root
        GameObject barGO = new GameObject("ScrollbarVisual", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGO.transform.SetParent(scrollViewTf, false);
        RectTransform barRt = barGO.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = Vector2.one;
        barRt.pivot = new Vector2(1f, 1f);
        barRt.offsetMin = new Vector2(-14f, 0f);
        barRt.offsetMax = new Vector2(-2f, 0f);

        Image barImage = barGO.GetComponent<Image>();
        barImage.color = new Color(0.15f, 0.12f, 0.05f, 1f);

        Scrollbar scrollbar = barGO.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.interactable = true;

        // Create sliding area
        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(barGO.transform, false);
        RectTransform slidingRt = slidingArea.GetComponent<RectTransform>();
        slidingRt.anchorMin = Vector2.zero;
        slidingRt.anchorMax = Vector2.one;
        slidingRt.offsetMin = Vector2.zero;
        slidingRt.offsetMax = Vector2.zero;

        // Create handle
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(1f, 0.2f);
        handleRt.offsetMin = Vector2.zero;
        handleRt.offsetMax = Vector2.zero;

        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(0.45f, 0.35f, 0.12f, 1f);

        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImage;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 4f;

        // Adjust viewport to make room for scrollbar
        RectTransform viewportRt = scrollViewTf.Find("Viewport")?.GetComponent<RectTransform>();
        if (viewportRt != null)
        {
            viewportRt.offsetMax = new Vector2(-16f, 0f);
        }
    }

    private void Start()
    {
        backdropButton.onClick.AddListener(Close);
        closeButton.onClick.AddListener(Close);
        questButton.onClick.AddListener(OnQuestButtonClicked);

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += UpdateNotificationDot;
            QuestManager.Instance.OnQuestsDropped  += UpdateNotificationDot;
            QuestManager.Instance.OnQuestCompleted += () => { if (isOpen) RefreshList(); };
        }

        UpdateNotificationDot();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= UpdateNotificationDot;
            QuestManager.Instance.OnQuestsDropped  -= UpdateNotificationDot;
        }
    }

    private void OnQuestButtonClicked()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shift && QuestPopupUITK.Instance != null)
            QuestPopupUITK.Instance.Open();
        else
            Open();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        RefreshAll();

        LeanTween.cancel(popupContainer.gameObject);
        LeanTween.cancel(canvasGroup.gameObject);

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration).setEase(easeType);
        LeanTween.scale(popupContainer, Vector3.one, fadeInDuration).setEase(easeType);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        LeanTween.cancel(popupContainer.gameObject);
        LeanTween.cancel(canvasGroup.gameObject);

        LeanTween.alphaCanvas(canvasGroup, 0f, fadeInDuration).setEase(easeType)
            .setOnComplete(() =>
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            });
        LeanTween.scale(popupContainer, Vector3.one * 0.8f, fadeInDuration).setEase(easeType);
    }

    public void RefreshAll()
    {
        RefreshMilestoneStrip();
        RefreshList();
        RefreshFooter();
    }

    public void RefreshMilestoneStrip()
    {
        if (QuestManager.Instance == null) return;

        int completed = QuestManager.Instance.QuestsCompletedThisWeek;
        bool[] claimed = QuestManager.Instance.WeeklyMilestonesClaimed;
        int[] thresholds = { 5, 10, 15, 20, 25, 30, 35, 40 };
        int[] gems       = { 1,  1,  2,  2,  2,  2,  2, 10 };

        weeklyCountLabel.text = $"<b><color=#FFD700>WEEKLY TRACK</color></b><align=right><color=#AAAAAA><size=14>{completed} / 40 quests  ·  resets Sun</size></color>";
        weeklyCountLabel.alignment = TextAlignmentOptions.Left;
        weeklyCountLabel.fontSize = 18;
        weeklyProgressBar.value = Mathf.Clamp01(completed / 40f);

        int nextTier = -1;
        for (int i = 0; i < 8; i++)
        {
            if (!claimed[i])
            {
                nextTier = i;
                break;
            }
        }

        for (int i = 0; i < milestoneChips.Count && i < 8; i++)
        {
            milestoneChips[i].Bind(i, thresholds[i], gems[i], claimed[i], i == nextTier);
        }
    }

    public void RefreshList()
    {
        if (QuestManager.Instance == null) return;

        foreach (GameObject row in spawnedRows)
            Destroy(row);
        spawnedRows.Clear();

        List<ActiveQuest> quests = QuestManager.Instance.GetActiveQuests();

        quests.Sort((a, b) =>
        {
            int scoreA = a.isCompleted ? 0 : (a.progress == 0 ? 2 : 1);
            int scoreB = b.isCompleted ? 0 : (b.progress == 0 ? 2 : 1);
            return scoreA.CompareTo(scoreB);
        });

        foreach (ActiveQuest quest in quests)
        {
            QuestData data = QuestManager.Instance.GetQuestData(quest.questID);
            if (data == null) { continue; }

            GameObject rowGO = Instantiate(questRowPrefab, questListContent);
            QuestRow row = rowGO.GetComponent<QuestRow>();
            try { row.Bind(quest, data); }
            catch (System.Exception e) { Debug.LogError($"[QuestPopup] Bind failed for '{quest.questID}': {e.Message}"); }
            spawnedRows.Add(rowGO);
        }

        Canvas.ForceUpdateCanvases();
        if (questListContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(questListContent.GetComponent<RectTransform>());

        RefreshFooter();
    }

    private void RefreshFooter()
    {
        if (QuestManager.Instance == null) return;

        DateTime nextDrop = QuestManager.Instance.GetNextDropTimeUtc();
        TimeSpan remaining = nextDrop - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        int hours = (int)remaining.TotalHours;
        int minutes = remaining.Minutes;

        string timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        footerText.text = $"Next drop in {timeStr}  \u00b7  {QuestManager.Instance.ActiveQuestCount}/10 slots";
        footerText.fontSize = 14;
        footerText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        footerText.alignment = TextAlignmentOptions.Center;
    }

    private void UpdateNotificationDot()
    {
        if (QuestManager.Instance == null) { notificationDot.SetActive(false); return; }
        notificationDot.SetActive(QuestManager.Instance.HasUnclaimedCompleted || QuestManager.Instance.HasNewDrops);
    }
}
