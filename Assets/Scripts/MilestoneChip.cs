using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MilestoneChip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tierLabel;
    [SerializeField] private TextMeshProUGUI gemLabel;
    [SerializeField] private TextMeshProUGUI stateIcon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;

    [Header("State Colors")]
    [SerializeField] private Color claimedBg     = new Color(0.227f, 0.180f, 0.063f, 1f); // #3a2e10
    [SerializeField] private Color claimedBorder  = new Color(0.416f, 0.353f, 0.125f, 1f); // #6a5a20
    [SerializeField] private Color nextBg         = new Color(0.165f, 0.122f, 0.039f, 1f); // #2a1f0a
    [SerializeField] private Color nextBorder      = new Color(1.000f, 0.843f, 0.000f, 1f); // #FFD700
    [SerializeField] private Color lockedBg        = new Color(0.102f, 0.078f, 0.035f, 1f); // #1a1409
    [SerializeField] private Color lockedBorder    = new Color(0.267f, 0.267f, 0.267f, 1f); // #444

    private int tierIndex;

    public void Bind(int index, int required, int gemReward, bool isClaimed, bool isNext)
    {
        tierIndex = index;
        tierLabel.text = required.ToString();
        gemLabel.text = "💎" + gemReward;

        bool isFinalTier = index == 7;

        if (isClaimed)
        {
            backgroundImage.color = claimedBg;
            borderImage.color = claimedBorder;
            stateIcon.text = "✓";
            stateIcon.color = new Color(0.545f, 0.765f, 0.290f, 1f); // green
            gemLabel.color = new Color(0.494f, 0.812f, 1.000f, 1f);
        }
        else if (isNext)
        {
            backgroundImage.color = nextBg;
            borderImage.color = nextBorder;
            stateIcon.text = "→";
            stateIcon.color = new Color(1f, 0.843f, 0f, 1f); // gold
            gemLabel.color = new Color(0.494f, 0.812f, 1.000f, 1f);
        }
        else
        {
            backgroundImage.color = lockedBg;
            borderImage.color = lockedBorder;
            stateIcon.text = isFinalTier ? "★" : "○";
            stateIcon.color = new Color(0.333f, 0.333f, 0.333f, 1f);
            gemLabel.color = new Color(0.267f, 0.400f, 0.533f, 1f);
        }

        GetComponent<Button>()?.onClick.RemoveAllListeners();
        GetComponent<Button>()?.onClick.AddListener(OnChipClicked);
    }

    private void OnChipClicked()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimMilestone(tierIndex))
        {
            QuestPopup.Instance?.RefreshMilestoneStrip();
        }
    }
}
