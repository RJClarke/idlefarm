using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Modal popup showing a 7-day reward calendar.
/// Each day shows: day name, coin amount, and status (claimed/available/missed/upcoming).
/// Dynamically generates day cells from code.
/// </summary>
public class DailyRewardPopup : MonoBehaviour
{
    public static DailyRewardPopup Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupContainer;
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI weeklyBonusText;
    [SerializeField] private RectTransform calendarContainer;

    [Header("Chest Button (opens this popup)")]
    [SerializeField] private Button chestButton;
    [SerializeField] private GameObject notificationDot;

    [Header("Day Cell Settings")]
    [SerializeField] private float cellWidth = 90f;
    [SerializeField] private float cellHeight = 110f;
    [SerializeField] private float cellSpacing = 8f;

    [Header("Colors")]
    [SerializeField] private Color claimedColor = new Color(0.3f, 0.7f, 0.3f, 1f);
    [SerializeField] private Color availableColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color missedColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
    [SerializeField] private Color upcomingColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
    [SerializeField] private Color todayOutlineColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    private bool isOpen = false;
    private GameObject[] dayCells = new GameObject[7];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        if (backdropButton != null)
            backdropButton.onClick.AddListener(Hide);
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimClicked);
        if (chestButton != null)
            chestButton.onClick.AddListener(Show);

        HideImmediate();

        // Hide dot initially, update once manager is ready
        if (notificationDot != null)
            notificationDot.SetActive(false);
    }

    private bool hasCheckedDot = false;

    private void Update()
    {
        if (!hasCheckedDot && DailyRewardManager.Instance != null)
        {
            hasCheckedDot = true;
            UpdateNotificationDot();
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
        if (backdropButton != null)
            backdropButton.onClick.RemoveListener(Hide);
        if (claimButton != null)
            claimButton.onClick.RemoveListener(OnClaimClicked);
        if (chestButton != null)
            chestButton.onClick.RemoveListener(Show);
    }

    public void Show()
    {
        if (isOpen) return;

        transform.SetAsLastSibling();
        BuildCalendar();
        UpdateClaimButton();

        isOpen = true;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (popupContainer != null)
        {
            popupContainer.localScale = Vector3.one * 0.8f;
            LeanTween.scale(popupContainer.gameObject, Vector3.one, fadeInDuration).setEase(easeType);
        }
        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration).setEase(easeType);
    }

    public void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (popupContainer != null)
            LeanTween.scale(popupContainer.gameObject, Vector3.one * 0.8f, fadeInDuration).setEase(easeType);
        LeanTween.alphaCanvas(canvasGroup, 0f, fadeInDuration).setEase(easeType);
    }

    private void UpdateNotificationDot()
    {
        if (notificationDot == null) return;
        bool show = DailyRewardManager.Instance != null && DailyRewardManager.Instance.CanClaimToday;
        notificationDot.SetActive(show);
    }

    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void OnClaimClicked()
    {
        if (DailyRewardManager.Instance == null) return;

        if (DailyRewardManager.Instance.ClaimToday())
        {
            BuildCalendar();
            UpdateClaimButton();
            UpdateNotificationDot();
        }
    }

    private void UpdateClaimButton()
    {
        if (DailyRewardManager.Instance == null || claimButton == null) return;

        bool canClaim = DailyRewardManager.Instance.CanClaimToday;
        claimButton.interactable = canClaim;

        if (claimButtonText != null)
        {
            if (canClaim)
            {
                int today = DailyRewardManager.Instance.GetTodayIndex();
                int reward = DailyRewardManager.Instance.DailyRewards[today];
                claimButtonText.text = $"Claim {reward} Coins";
            }
            else
            {
                claimButtonText.text = "Already Claimed";
            }
        }
    }

    private void BuildCalendar()
    {
        if (DailyRewardManager.Instance == null || calendarContainer == null) return;

        // Clear existing cells
        for (int i = 0; i < 7; i++)
        {
            if (dayCells[i] != null) Destroy(dayCells[i]);
        }

        int[] rewards = DailyRewardManager.Instance.DailyRewards;
        int todayIndex = DailyRewardManager.Instance.GetTodayIndex();

        // Layout: 7 cells in a row, centered
        float totalWidth = 7 * cellWidth + 6 * cellSpacing;
        float startX = -totalWidth / 2f + cellWidth / 2f;

        for (int i = 0; i < 7; i++)
        {
            DayStatus status = DailyRewardManager.Instance.GetDayStatus(i);
            int reward = (i < rewards.Length) ? rewards[i] : 0;

            GameObject cell = CreateDayCell(i, reward, status, i == todayIndex);
            RectTransform rt = cell.GetComponent<RectTransform>();
            rt.SetParent(calendarContainer, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellWidth, cellHeight);
            rt.anchoredPosition = new Vector2(startX + i * (cellWidth + cellSpacing), 0);

            dayCells[i] = cell;
        }

        // Update weekly bonus text
        if (weeklyBonusText != null)
        {
            int claimed = DailyRewardManager.Instance.ClaimedCount;
            if (DailyRewardManager.Instance.EarnedWeeklyBonus)
                weeklyBonusText.text = $"<color=#FFD700>Weekly Bonus Earned! +{DailyRewardManager.Instance.WeeklyBonusReward} Coins</color>";
            else
                weeklyBonusText.text = $"Claim all 7 days for +{DailyRewardManager.Instance.WeeklyBonusReward} bonus coins ({claimed}/7)";
        }
    }

    private GameObject CreateDayCell(int dayIndex, int reward, DayStatus status, bool isToday)
    {
        GameObject cell = new GameObject($"Day_{dayIndex}");

        // Background
        Image bg = cell.AddComponent<Image>();
        switch (status)
        {
            case DayStatus.Claimed:   bg.color = claimedColor; break;
            case DayStatus.Available: bg.color = availableColor; break;
            case DayStatus.Missed:    bg.color = missedColor; break;
            case DayStatus.Upcoming:  bg.color = upcomingColor; break;
        }

        // Day name label (top)
        GameObject dayNameObj = new GameObject("DayName");
        RectTransform dnrt = dayNameObj.AddComponent<RectTransform>();
        dnrt.SetParent(cell.transform, false);
        dnrt.anchorMin = new Vector2(0, 0.7f);
        dnrt.anchorMax = new Vector2(1, 1);
        dnrt.offsetMin = Vector2.zero;
        dnrt.offsetMax = Vector2.zero;
        dayNameObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI dayNameText = dayNameObj.AddComponent<TextMeshProUGUI>();
        dayNameText.text = DailyRewardManager.Instance.GetDayName(dayIndex);
        dayNameText.fontSize = 18;
        dayNameText.alignment = TextAlignmentOptions.Center;
        dayNameText.color = Color.white;
        if (isToday) dayNameText.fontStyle = FontStyles.Bold;

        // Reward amount (center)
        GameObject rewardObj = new GameObject("Reward");
        RectTransform rrt = rewardObj.AddComponent<RectTransform>();
        rrt.SetParent(cell.transform, false);
        rrt.anchorMin = new Vector2(0, 0.25f);
        rrt.anchorMax = new Vector2(1, 0.7f);
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;
        rewardObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI rewardText = rewardObj.AddComponent<TextMeshProUGUI>();
        rewardText.text = $"{reward}";
        rewardText.fontSize = 24;
        rewardText.fontStyle = FontStyles.Bold;
        rewardText.alignment = TextAlignmentOptions.Center;
        rewardText.color = Color.white;

        // Status icon (bottom)
        GameObject statusObj = new GameObject("Status");
        RectTransform srt = statusObj.AddComponent<RectTransform>();
        srt.SetParent(cell.transform, false);
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 0.25f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        statusObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 14;
        statusText.alignment = TextAlignmentOptions.Center;

        switch (status)
        {
            case DayStatus.Claimed:
                statusText.text = "Claimed";
                statusText.color = new Color(0.8f, 1f, 0.8f);
                break;
            case DayStatus.Available:
                statusText.text = "Today!";
                statusText.color = new Color(1f, 1f, 0.8f);
                break;
            case DayStatus.Missed:
                statusText.text = "Missed";
                statusText.color = new Color(0.7f, 0.7f, 0.7f);
                break;
            case DayStatus.Upcoming:
                statusText.text = "";
                statusText.color = new Color(0.6f, 0.6f, 0.6f);
                break;
        }

        return cell;
    }
}
