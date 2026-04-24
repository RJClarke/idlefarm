using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

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
    [SerializeField] private List<MilestoneChip> milestoneChips; // 8 chips, wired in inspector

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
    }

    private void Start()
    {
        backdropButton.onClick.AddListener(Close);
        closeButton.onClick.AddListener(Close);
        questButton.onClick.AddListener(Open);

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

        weeklyCountLabel.text = $"{completed} / 40 quests · resets Sun";
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

        // Sort: completed first, then in-progress, then new (no progress yet)
        quests.Sort((a, b) =>
        {
            int scoreA = a.isCompleted ? 0 : (a.progress == 0 ? 2 : 1);
            int scoreB = b.isCompleted ? 0 : (b.progress == 0 ? 2 : 1);
            return scoreA.CompareTo(scoreB);
        });

        foreach (ActiveQuest quest in quests)
        {
            QuestData data = QuestManager.Instance.GetQuestData(quest.questID);
            if (data == null) continue;

            GameObject rowGO = Instantiate(questRowPrefab, questListContent);
            QuestRow row = rowGO.GetComponent<QuestRow>();
            row.Bind(quest, data);
            spawnedRows.Add(rowGO);
        }

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
        footerText.text = $"Next drop in {timeStr} · {QuestManager.Instance.ActiveQuestCount} / 10 slots used";
    }

    private void UpdateNotificationDot()
    {
        if (QuestManager.Instance == null) { notificationDot.SetActive(false); return; }
        notificationDot.SetActive(QuestManager.Instance.HasUnclaimedCompleted || QuestManager.Instance.HasNewDrops);
    }
}
