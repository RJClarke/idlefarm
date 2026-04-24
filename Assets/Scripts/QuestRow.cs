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
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;

    [Header("State Colors")]
    [SerializeField] private Color inProgressBg = new Color(0.122f, 0.102f, 0.039f, 1f);      // #1f1a0a
    [SerializeField] private Color completedBg  = new Color(0.102f, 0.180f, 0.063f, 1f);      // #1a2e10
    [SerializeField] private Color newBg        = new Color(0.102f, 0.063f, 0.125f, 1f);      // #1a1020
    [SerializeField] private Color inProgressBorder = new Color(0.227f, 0.180f, 0.063f, 1f);  // #3a2e10
    [SerializeField] private Color completedBorder  = new Color(0.227f, 0.376f, 0.125f, 1f);  // #3a6020
    [SerializeField] private Color newBorder        = new Color(0.290f, 0.125f, 0.376f, 1f);  // #4a2060

    [Header("Bar Colors")]
    [SerializeField] private Color harvestColor = new Color(0.545f, 0.765f, 0.290f, 1f); // #8BC34A green
    [SerializeField] private Color waterColor   = new Color(0.290f, 0.624f, 0.875f, 1f); // #4a9fdf blue
    [SerializeField] private Color newColor     = new Color(0.690f, 0.502f, 1.000f, 1f); // #b080ff purple
    [SerializeField] private Color defaultColor = new Color(0.545f, 0.765f, 0.290f, 1f); // green

    private string currentQuestID;

    public void Bind(ActiveQuest quest, QuestData data)
    {
        currentQuestID = quest.questID;
        questNameText.text = data.displayName;
        descriptionText.text = data.description;
        rewardText.text = "🪙" + data.coinReward;

        float fill = data.targetCount > 0 ? (float)quest.progress / data.targetCount : 0f;
        progressBar.value = fill;

        bool isNew = quest.progress == 0;

        // State: completed
        if (quest.isCompleted)
        {
            backgroundImage.color = completedBg;
            borderImage.color = completedBorder;
            progressText.text = "";
            completeLabel.gameObject.SetActive(true);
            newBadgeText.gameObject.SetActive(false);
            claimButton.gameObject.SetActive(true);
            claimButtonText.text = "Claim\n🪙" + data.coinReward;
            questNameText.color = new Color(0.545f, 0.765f, 0.290f, 1f);
            progressBar.fillRect.GetComponent<Image>().color = harvestColor;
        }
        // State: new (no progress)
        else if (isNew)
        {
            backgroundImage.color = newBg;
            borderImage.color = newBorder;
            progressText.text = "0 / " + data.targetCount;
            completeLabel.gameObject.SetActive(false);
            newBadgeText.gameObject.SetActive(true);
            claimButton.gameObject.SetActive(false);
            questNameText.color = Color.white;
            progressBar.fillRect.GetComponent<Image>().color = newColor;
        }
        // State: in progress
        else
        {
            backgroundImage.color = inProgressBg;
            borderImage.color = inProgressBorder;
            progressText.text = quest.progress + " / " + data.targetCount;
            completeLabel.gameObject.SetActive(false);
            newBadgeText.gameObject.SetActive(false);
            claimButton.gameObject.SetActive(false);
            questNameText.color = Color.white;

            progressBar.fillRect.GetComponent<Image>().color = data.objectiveType switch
            {
                QuestObjectiveType.WaterPlants => waterColor,
                _ => defaultColor
            };
        }

        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(OnClaimClicked);
    }

    private void OnClaimClicked()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimQuest(currentQuestID))
        {
            QuestPopup.Instance?.RefreshList();
        }
    }
}
