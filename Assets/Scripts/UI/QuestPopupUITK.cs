using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class QuestPopupUITK : MonoBehaviour
{
    public static QuestPopupUITK Instance { get; private set; }

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset questRowTemplate;
    [SerializeField] private VisualTreeAsset milestoneChipTemplate;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement popupContainer;
    private VisualElement backdrop;
    private Button closeButton;
    private Label weeklyCountLabel;
    private VisualElement weeklyProgressFill;
    private VisualElement milestoneStrip;
    private ScrollView questList;
    private Label footerText;

    private bool isOpen;
    private readonly List<VisualElement> spawnedRows = new List<VisualElement>();
    private readonly List<VisualElement> spawnedChips = new List<VisualElement>();
    private IVisualElementScheduledItem livePollHandle;

    private static readonly int[] MilestoneThresholds = { 5, 10, 15, 20, 25, 30, 35, 40 };
    private static readonly int[] MilestoneGems       = { 1, 1, 2, 2, 2, 2, 2, 10 };

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
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += OnQuestStateChanged;
            QuestManager.Instance.OnQuestsDropped  += OnQuestStateChanged;
            QuestManager.Instance.OnMilestoneClaimed += OnQuestStateChanged;
        }
    }

    private void OnDisable()
    {
        StopLivePolling();
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= OnQuestStateChanged;
            QuestManager.Instance.OnQuestsDropped  -= OnQuestStateChanged;
            QuestManager.Instance.OnMilestoneClaimed -= OnQuestStateChanged;
        }
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[QuestPopupUITK] rootVisualElement is null"); return; }

        // Pass clicks through to uGUI when the popup is closed.
        root.pickingMode = PickingMode.Ignore;

        popupRoot          = root.Q<VisualElement>("popup-root");
        popupContainer     = root.Q<VisualElement>("popup-container");
        backdrop           = root.Q<VisualElement>("backdrop");
        closeButton        = root.Q<Button>("close-button");
        weeklyCountLabel   = root.Q<Label>("weekly-count");
        weeklyProgressFill = root.Q<VisualElement>("weekly-progress-fill");
        milestoneStrip     = root.Q<VisualElement>("milestone-strip");
        questList          = root.Q<ScrollView>("quest-list");
        footerText         = root.Q<Label>("footer-text");

        WarnIfNull(popupRoot,          "popup-root");
        WarnIfNull(popupContainer,     "popup-container");
        WarnIfNull(backdrop,           "backdrop");
        WarnIfNull(closeButton,        "close-button");
        WarnIfNull(weeklyCountLabel,   "weekly-count");
        WarnIfNull(weeklyProgressFill, "weekly-progress-fill");
        WarnIfNull(milestoneStrip,     "milestone-strip");
        WarnIfNull(questList,          "quest-list");
        WarnIfNull(footerText,         "footer-text");
    }

    private static void WarnIfNull(object obj, string name)
    {
        if (obj == null) Debug.LogWarning($"[QuestPopupUITK] element '{name}' not found in UXML");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.clicked += Close;
        if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        RefreshAll();
        StartLivePolling();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        StopLivePolling();
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

    private void StartLivePolling()
    {
        if (livePollHandle != null || root == null) return;
        livePollHandle = root.schedule.Execute(PollLiveProgress).Every(250);
    }

    private void StopLivePolling()
    {
        if (livePollHandle == null) return;
        livePollHandle.Pause();
        livePollHandle = null;
    }

    private void PollLiveProgress()
    {
        if (!isOpen || QuestManager.Instance == null) return;

        foreach (VisualElement rowVe in spawnedRows)
        {
            ActiveQuest quest = rowVe.userData as ActiveQuest;
            if (quest == null) continue;

            QuestData data = QuestManager.Instance.GetQuestData(quest.questID);
            if (data == null) continue;

            // If the quest crossed the completion threshold, do a full rebuild so the
            // row switches to the "completed" visual state with the claim button.
            bool shownAsComplete = rowVe.ClassListContains("quest-row--completed");
            if (quest.isCompleted != shownAsComplete)
            {
                RefreshList();
                return;
            }

            // Otherwise just update the bar fill and progress text in place.
            VisualElement fill = rowVe.Q<VisualElement>("quest-progress-fill");
            float pct = data.targetCount > 0 ? Mathf.Clamp01((float)quest.progress / data.targetCount) : 0f;
            if (fill != null) fill.style.width = new Length(pct * 100f, LengthUnit.Percent);

            Label progressLabel = rowVe.Q<Label>("progress-text");
            if (progressLabel != null && progressLabel.style.display.value != DisplayStyle.None)
                progressLabel.text = quest.progress + " / " + data.targetCount;
        }
    }

    private void OnQuestStateChanged()
    {
        if (isOpen) RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshMilestoneStrip();
        RefreshList();
        RefreshFooter();
    }

    public void RefreshMilestoneStrip()
    {
        if (QuestManager.Instance == null || milestoneStrip == null) return;

        int completed = QuestManager.Instance.QuestsCompletedThisWeek;
        bool[] claimed = QuestManager.Instance.WeeklyMilestonesClaimed;

        if (weeklyCountLabel != null)
            weeklyCountLabel.text = $"{completed} / 40 quests · resets Sun";

        if (weeklyProgressFill != null)
            weeklyProgressFill.style.width = new Length(Mathf.Clamp01(completed / 40f) * 100f, LengthUnit.Percent);

        // Every unclaimed tier whose threshold the user has hit is "next" (claimable).
        // Tiers below their threshold stay locked; claimed tiers stay claimed.

        foreach (VisualElement old in spawnedChips) old.RemoveFromHierarchy();
        spawnedChips.Clear();

        if (milestoneChipTemplate == null)
        {
            Debug.LogWarning("[QuestPopupUITK] milestoneChipTemplate not assigned");
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            TemplateContainer chip = milestoneChipTemplate.Instantiate();
            milestoneStrip.Add(chip);
            VisualElement chipRoot = chip.Q(className: "chip") ?? chip.contentContainer;

            int tier = i;
            int threshold = MilestoneThresholds[i];
            int gemReward = MilestoneGems[i];
            bool isClaimed = claimed[i];
            bool isNext = !isClaimed && completed >= threshold;
            bool isFinal = i == 7;

            Label iconLabel = chip.Q<Label>("state-icon");
            Label tierLabel = chip.Q<Label>("tier-label");
            Label gemLabel  = chip.Q<Label>("gem-label");

            if (tierLabel != null) tierLabel.text = threshold.ToString();
            if (gemLabel != null)  gemLabel.text  = "◆" + gemReward;

            chipRoot.RemoveFromClassList("chip--claimed");
            chipRoot.RemoveFromClassList("chip--next");
            chipRoot.RemoveFromClassList("chip--locked");
            chipRoot.RemoveFromClassList("chip--final");

            if (isClaimed)
            {
                chipRoot.AddToClassList("chip--claimed");
                if (iconLabel != null) iconLabel.text = "✓";
            }
            else if (isNext)
            {
                chipRoot.AddToClassList("chip--next");
                if (iconLabel != null) iconLabel.text = "→";
            }
            else
            {
                chipRoot.AddToClassList("chip--locked");
                if (isFinal) chipRoot.AddToClassList("chip--final");
                if (iconLabel != null) iconLabel.text = isFinal ? "★" : "○";
            }

            Button chipButton = chipRoot as Button;
            if (chipButton != null)
            {
                if (isNext)
                {
                    chipButton.SetEnabled(true);
                    chipButton.RegisterCallback<ClickEvent>(_ => OnChipClicked(tier));
                }
                else
                {
                    chipButton.SetEnabled(false);
                }
            }

            spawnedChips.Add(chip);
        }
    }

    public void RefreshList()
    {
        if (QuestManager.Instance == null || questList == null) return;

        foreach (VisualElement old in spawnedRows) old.RemoveFromHierarchy();
        spawnedRows.Clear();

        if (questRowTemplate == null)
        {
            Debug.LogWarning("[QuestPopupUITK] questRowTemplate not assigned");
            return;
        }

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
            if (data == null) continue;
            TemplateContainer row = questRowTemplate.Instantiate();
            row.userData = quest;
            BindRow(row, quest, data);
            questList.Add(row);
            spawnedRows.Add(row);
        }
    }

    private void BindRow(TemplateContainer row, ActiveQuest quest, QuestData data)
    {
        VisualElement rowRoot = row.Q(className: "quest-row") ?? row.contentContainer;

        Label nameLabel         = row.Q<Label>("quest-name");
        Label descLabel         = row.Q<Label>("quest-description");
        Label completeLabel     = row.Q<Label>("complete-label");
        Label progressLabel     = row.Q<Label>("progress-text");
        Label rewardLabel       = row.Q<Label>("reward-text");
        VisualElement rewardRow = row.Q<VisualElement>("reward-row");
        Button claimButton      = row.Q<Button>("claim-button");
        Label claimRewardLbl    = row.Q<Label>("claim-button-reward");
        VisualElement newBadge  = row.Q<VisualElement>("new-badge");
        VisualElement progFill  = row.Q<VisualElement>("quest-progress-fill");

        if (nameLabel != null)   nameLabel.text   = data.displayName;
        if (descLabel != null)   descLabel.text   = data.description;
        if (rewardLabel != null) rewardLabel.text = data.coinReward.ToString();

        bool isComplete = quest.isCompleted;
        bool isNew = quest.progress == 0 && !quest.isCompleted;

        rowRoot.RemoveFromClassList("quest-row--in-progress");
        rowRoot.RemoveFromClassList("quest-row--completed");
        rowRoot.RemoveFromClassList("quest-row--new");

        float fill = data.targetCount > 0 ? Mathf.Clamp01((float)quest.progress / data.targetCount) : 0f;
        if (progFill != null) progFill.style.width = new Length(fill * 100f, LengthUnit.Percent);

        ClearObjectiveClasses(rowRoot);
        rowRoot.AddToClassList("quest-row--obj-" + data.objectiveType.ToString().ToLower());

        if (isComplete)
        {
            rowRoot.AddToClassList("quest-row--completed");
            if (completeLabel != null) completeLabel.style.display = DisplayStyle.Flex;
            if (descLabel != null)     descLabel.style.display     = DisplayStyle.None;
            if (progressLabel != null) progressLabel.style.display = DisplayStyle.None;
            if (rewardRow != null)     rewardRow.style.display     = DisplayStyle.None;
            if (newBadge != null)      newBadge.style.display      = DisplayStyle.None;
            if (claimButton != null)
            {
                claimButton.style.display = DisplayStyle.Flex;
                if (claimRewardLbl != null) claimRewardLbl.text = data.coinReward.ToString();
                string capturedID = quest.questID;
                int capturedReward = data.coinReward;
                VisualElement capturedRow = rowRoot;
                Button capturedButton = claimButton;
                claimButton.RegisterCallback<ClickEvent>(_ => OnClaimClicked(capturedID, capturedRow, capturedButton, capturedReward));
            }
        }
        else if (isNew)
        {
            rowRoot.AddToClassList("quest-row--new");
            if (completeLabel != null) completeLabel.style.display = DisplayStyle.None;
            if (descLabel != null)     descLabel.style.display     = DisplayStyle.Flex;
            if (progressLabel != null) { progressLabel.style.display = DisplayStyle.Flex; progressLabel.text = "0 / " + data.targetCount; }
            if (rewardRow != null)     rewardRow.style.display     = DisplayStyle.Flex;
            if (newBadge != null)      newBadge.style.display      = DisplayStyle.Flex;
            if (claimButton != null)   claimButton.style.display   = DisplayStyle.None;
        }
        else
        {
            rowRoot.AddToClassList("quest-row--in-progress");
            if (completeLabel != null) completeLabel.style.display = DisplayStyle.None;
            if (descLabel != null)     descLabel.style.display     = DisplayStyle.Flex;
            if (progressLabel != null) { progressLabel.style.display = DisplayStyle.Flex; progressLabel.text = quest.progress + " / " + data.targetCount; }
            if (rewardRow != null)     rewardRow.style.display     = DisplayStyle.Flex;
            if (newBadge != null)      newBadge.style.display      = DisplayStyle.None;
            if (claimButton != null)   claimButton.style.display   = DisplayStyle.None;
        }
    }

    private static void ClearObjectiveClasses(VisualElement rowRoot)
    {
        foreach (QuestObjectiveType type in Enum.GetValues(typeof(QuestObjectiveType)))
            rowRoot.RemoveFromClassList("quest-row--obj-" + type.ToString().ToLower());
    }

    private void RefreshFooter()
    {
        if (QuestManager.Instance == null || footerText == null) return;
        DateTime nextDrop = QuestManager.Instance.GetNextDropTimeUtc();
        TimeSpan remaining = nextDrop - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        int hours = (int)remaining.TotalHours;
        int minutes = remaining.Minutes;
        string timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        footerText.text = $"Next drop in {timeStr}  ·  {QuestManager.Instance.ActiveQuestCount}/10 slots";
    }

    private void OnClaimClicked(string questID, VisualElement row, Button claimButton, int coinReward)
    {
        // Prevent double-claim while the animation runs.
        if (claimButton != null) claimButton.SetEnabled(false);

        // Spawn the "+coins" floating text near the claim button.
        SpawnClaimFloat(claimButton ?? row, coinReward);

        // Fade and collapse the row. USS handles the transition.
        if (row != null) row.AddToClassList("quest-row--claiming");

        // After the visual animation, actually claim. The state-change event will
        // trigger RefreshList which rebuilds the (now shorter) list.
        IVisualElementScheduledItem item = (row ?? root)?.schedule.Execute(() =>
        {
            if (QuestManager.Instance != null) QuestManager.Instance.TryClaimQuest(questID);
        });
        item?.StartingIn(380);
    }

    private void SpawnClaimFloat(VisualElement source, int coinAmount)
    {
        // Parent to popupRoot (no overflow clipping) instead of popupContainer.
        if (popupRoot == null || source == null) return;

        Label floatLabel = new Label($"+{coinAmount}");
        floatLabel.AddToClassList("claim-float");
        floatLabel.pickingMode = PickingMode.Ignore;

        // Position relative to popupRoot using world bounds. Pivot is top-left of the
        // label, so shift left by half the label's expected width (~25px) to center.
        Rect srcBound = source.worldBound;
        Rect rootBound = popupRoot.worldBound;
        float left = srcBound.center.x - rootBound.x - 25f;
        float top  = srcBound.center.y - rootBound.y - 20f;

        floatLabel.style.left = left;
        floatLabel.style.top = top;

        popupRoot.Add(floatLabel);

        // Defer adding the .animating class so the transition plays from the initial state.
        floatLabel.schedule.Execute(() => floatLabel.AddToClassList("claim-float--animating")).StartingIn(10);

        // Remove the label after the animation completes.
        floatLabel.schedule.Execute(() => floatLabel.RemoveFromHierarchy()).StartingIn(1300);
    }

    private void OnChipClicked(int tier)
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimMilestone(tier))
            RefreshMilestoneStrip();
    }
}
