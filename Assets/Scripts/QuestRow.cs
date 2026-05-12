using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestRow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI newBadgeText;
    [SerializeField] private TextMeshProUGUI completeLabel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private Image progressBarBg;
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private LayoutElement layoutElement;

    [Header("State Colors")]
    [SerializeField] private Color inProgressBg = new Color(0.18f, 0.14f, 0.06f, 1f);
    [SerializeField] private Color completedBg  = new Color(0.09f, 0.20f, 0.06f, 1f);
    [SerializeField] private Color newBg        = new Color(0.18f, 0.14f, 0.06f, 1f);
    [SerializeField] private Color inProgressBorder = new Color(0.35f, 0.27f, 0.10f, 1f);
    [SerializeField] private Color completedBorder  = new Color(0.28f, 0.55f, 0.16f, 1f);
    [SerializeField] private Color newBorder        = new Color(0.45f, 0.30f, 0.12f, 1f);

    [Header("Bar Colors")]
    [SerializeField] private Color harvestColor = new Color(0.545f, 0.765f, 0.290f, 1f);
    [SerializeField] private Color waterColor   = new Color(0.290f, 0.624f, 0.875f, 1f);
    [SerializeField] private Color newColor     = new Color(0.690f, 0.502f, 0.300f, 1f);
    [SerializeField] private Color defaultColor = new Color(0.545f, 0.765f, 0.290f, 1f);
    [SerializeField] private Color barBgColor   = new Color(0.08f, 0.06f, 0.03f, 1f);

    private string currentQuestID;

    private void Awake()
    {
        AutoWire();
        SetupLayout();
    }

    private void AutoWire()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (borderImage == null)     borderImage     = transform.Find("BorderImage")?.GetComponent<Image>();
        if (questNameText == null)   questNameText   = transform.Find("QuestNameText")?.GetComponent<TextMeshProUGUI>();
        if (descriptionText == null) descriptionText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (progressText == null)    progressText    = transform.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();
        if (rewardText == null)      rewardText      = transform.Find("RewardText")?.GetComponent<TextMeshProUGUI>();
        if (newBadgeText == null)    newBadgeText    = transform.Find("NewBadge")?.GetComponent<TextMeshProUGUI>();
        if (completeLabel == null)   completeLabel   = transform.Find("CompleteLabel")?.GetComponent<TextMeshProUGUI>();
        if (progressBar == null)     progressBar     = transform.Find("ProgressBar")?.GetComponent<Slider>();
        if (progressBarFill == null) progressBarFill = transform.Find("ProgressBar/Fill Area/Fill")?.GetComponent<Image>();
        if (progressBarBg == null)   progressBarBg   = transform.Find("ProgressBar/Background")?.GetComponent<Image>();
        if (progressBar != null && progressBar.fillRect == null)
        {
            Transform fillTf = transform.Find("ProgressBar/Fill Area/Fill");
            if (fillTf != null) progressBar.fillRect = fillTf.GetComponent<RectTransform>();
        }
        if (claimButton == null)     claimButton     = transform.Find("ClaimButton")?.GetComponent<Button>();
        if (claimButtonText == null) claimButtonText = transform.Find("ClaimButton/ClaimButtonText")?.GetComponent<TextMeshProUGUI>();
        if (layoutElement == null)   layoutElement   = GetComponent<LayoutElement>();
    }

    private void SetupLayout()
    {
        Stretch(GetComponent<RectTransform>());

        borderImage?.transform.SetAsFirstSibling();
        if (borderImage) Stretch(borderImage.rectTransform, -3f);

        if (layoutElement)
        {
            layoutElement.preferredHeight = 90;
            layoutElement.minHeight = 80;
        }

        if (newBadgeText) SetRect(newBadgeText.rectTransform, 0f, 0.72f, 0.25f, 1f, 8f, -4f, 0f, -4f);

        if (questNameText) SetRect(questNameText.rectTransform, 0f, 0.48f, 0.65f, 0.94f, 10f, 0f, -4f, 0f);

        if (descriptionText) SetRect(descriptionText.rectTransform, 0f, 0.20f, 0.65f, 0.50f, 10f, 0f, -4f, 0f);

        if (completeLabel) SetRect(completeLabel.rectTransform, 0f, 0.20f, 0.65f, 0.50f, 10f, 0f, -4f, 0f);

        if (progressText) SetRect(progressText.rectTransform, 0.63f, 0.55f, 1f, 0.95f, 0f, 0f, -10f, -4f);

        if (rewardText) SetRect(rewardText.rectTransform, 0.63f, 0.25f, 1f, 0.58f, 0f, 0f, -10f, 0f);

        RectTransform barRt = progressBar?.GetComponent<RectTransform>();
        if (barRt) SetRect(barRt, 0f, 0f, 1f, 0.20f, 10f, 2f, -10f, 2f);

        if (progressBar != null)
        {
            Transform bgTf = progressBar.transform.Find("Background");
            if (bgTf) Stretch(bgTf.GetComponent<RectTransform>());
            Transform fillAreaTf = progressBar.transform.Find("Fill Area");
            if (fillAreaTf) Stretch(fillAreaTf.GetComponent<RectTransform>());
            if (progressBarFill) Stretch(progressBarFill.rectTransform);
            Transform handleArea = progressBar.transform.Find("Handle Slide Area");
            if (handleArea) handleArea.gameObject.SetActive(false);
        }

        RectTransform btnRt = claimButton?.GetComponent<RectTransform>();
        if (btnRt) SetRect(btnRt, 0.62f, 0.08f, 1f, 0.92f, 4f, 0f, -10f, 0f);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRect(RectTransform rt, float axMin, float ayMin, float axMax, float ayMax,
        float oxMin, float oyMin, float oxMax, float oyMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(axMin, ayMin);
        rt.anchorMax = new Vector2(axMax, ayMax);
        rt.offsetMin = new Vector2(oxMin, oyMin);
        rt.offsetMax = new Vector2(oxMax, oyMax);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    public void Bind(ActiveQuest quest, QuestData data)
    {
        currentQuestID = quest.questID;

        if (questNameText) { questNameText.text = data.displayName; questNameText.fontSize = 18; questNameText.fontWeight = FontWeight.Bold; }
        if (descriptionText) { descriptionText.text = data.description; descriptionText.fontSize = 13; descriptionText.color = new Color(0.7f, 0.7f, 0.7f, 1f); }
        if (rewardText) { rewardText.text = "\U0001fa99" + data.coinReward; rewardText.fontSize = 16; rewardText.alignment = TextAlignmentOptions.Right; rewardText.fontWeight = FontWeight.Bold; }

        float fill = data.targetCount > 0 ? (float)quest.progress / data.targetCount : 0f;
        if (progressBar) progressBar.value = fill;
        if (progressBarBg) progressBarBg.color = barBgColor;

        bool isNew = quest.progress == 0 && !quest.isCompleted;
        string progressStr = quest.progress + " / " + data.targetCount;

        if (quest.isCompleted)
        {
            if (backgroundImage) backgroundImage.color = completedBg;
            if (borderImage)     borderImage.color     = completedBorder;
            if (progressText)    progressText.gameObject.SetActive(false);
            if (completeLabel)   { completeLabel.gameObject.SetActive(true); completeLabel.text = "Complete!"; completeLabel.color = new Color(0.545f, 0.765f, 0.290f, 1f); completeLabel.fontSize = 14; completeLabel.fontWeight = FontWeight.Bold; }
            if (newBadgeText)    newBadgeText.gameObject.SetActive(false);
            if (claimButton)     claimButton.gameObject.SetActive(true);
            if (claimButtonText)
            {
                claimButtonText.text = "Claim\n\U0001fa99 " + data.coinReward;
                claimButtonText.fontSize = 14;
                claimButtonText.fontWeight = FontWeight.Bold;
                claimButtonText.alignment = TextAlignmentOptions.Center;
                claimButtonText.enableWordWrapping = true;
                claimButtonText.lineSpacing = -8;
            }
            if (questNameText)   questNameText.color = new Color(0.545f, 0.765f, 0.290f, 1f);
            if (progressBarFill) progressBarFill.color = harvestColor;
            if (rewardText)      rewardText.gameObject.SetActive(false);
        }
        else if (isNew)
        {
            if (backgroundImage) backgroundImage.color = newBg;
            if (borderImage)     borderImage.color     = newBorder;
            if (progressText)    { progressText.gameObject.SetActive(true); progressText.text = "0 / " + data.targetCount; progressText.fontSize = 14; progressText.alignment = TextAlignmentOptions.Right; progressText.color = new Color(0.7f, 0.7f, 0.7f, 1f); }
            if (completeLabel)   completeLabel.gameObject.SetActive(false);
            if (newBadgeText)    { newBadgeText.gameObject.SetActive(true); newBadgeText.text = "NEW"; newBadgeText.fontSize = 12; newBadgeText.fontWeight = FontWeight.Bold; newBadgeText.color = new Color(0.690f, 0.502f, 0.300f, 1f); }
            if (claimButton)     claimButton.gameObject.SetActive(false);
            if (questNameText)   questNameText.color = Color.white;
            if (progressBarFill) progressBarFill.color = newColor;
            if (rewardText)      rewardText.gameObject.SetActive(true);
        }
        else
        {
            if (backgroundImage) backgroundImage.color = inProgressBg;
            if (borderImage)     borderImage.color     = inProgressBorder;
            if (progressText)    { progressText.gameObject.SetActive(true); progressText.text = progressStr; progressText.fontSize = 14; progressText.alignment = TextAlignmentOptions.Right; progressText.color = new Color(0.7f, 0.7f, 0.7f, 1f); }
            if (completeLabel)   completeLabel.gameObject.SetActive(false);
            if (newBadgeText)    newBadgeText.gameObject.SetActive(false);
            if (claimButton)     claimButton.gameObject.SetActive(false);
            if (questNameText)   questNameText.color = Color.white;
            if (rewardText)      rewardText.gameObject.SetActive(true);

            if (progressBarFill)
                progressBarFill.color = data.objectiveType switch
                {
                    QuestObjectiveType.WaterPlants => waterColor,
                    _ => defaultColor
                };
        }

        if (claimButton)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimClicked);
            Image btnImg = claimButton.GetComponent<Image>();
            if (btnImg) btnImg.color = new Color(0.137f, 0.545f, 0.137f, 1f);
        }
    }

    private void OnClaimClicked()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimQuest(currentQuestID))
            QuestPopup.Instance?.RefreshList();
    }
}
