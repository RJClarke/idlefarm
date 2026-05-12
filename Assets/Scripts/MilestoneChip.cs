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

    private void Awake()
    {
        // Border must render behind text — move it to first sibling before stretching
        borderImage?.transform.SetAsFirstSibling();
        StretchToFill(borderImage?.rectTransform);

        // Center all text elements within the chip
        float midY = 0.50f;
        float iconH = 0.40f;
        float labelH = 0.35f;
        float gemH = 0.30f;

        SetAnchorSlice(stateIcon?.rectTransform,  0f, midY + iconH * 0.5f, 1f, 1f);
        SetAnchorSlice(tierLabel?.rectTransform,  0f, midY - labelH * 0.5f, 1f, midY + labelH * 0.5f);
        SetAnchorSlice(gemLabel?.rectTransform,   0f, 0f, 1f, gemH);

        // Set centered alignment on all text
        if (stateIcon != null) stateIcon.alignment = TextAlignmentOptions.Center;
        if (tierLabel != null) tierLabel.alignment = TextAlignmentOptions.Center;
        if (gemLabel != null)  gemLabel.alignment  = TextAlignmentOptions.Center;

        stateIcon.fontSize = 18;
        tierLabel.fontSize = 14;
        gemLabel.fontSize  = 11;
    }

    private static void StretchToFill(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchorSlice(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    [Header("State Colors")]
    [SerializeField] private Color claimedBg     = new Color(0.118f, 0.220f, 0.082f, 1f);
    [SerializeField] private Color claimedBorder  = new Color(0.275f, 0.545f, 0.161f, 1f);
    [SerializeField] private Color nextBg         = new Color(0.200f, 0.145f, 0.039f, 1f);
    [SerializeField] private Color nextBorder      = new Color(1.000f, 0.722f, 0.000f, 1f);
    [SerializeField] private Color lockedBg        = new Color(0.102f, 0.078f, 0.035f, 1f);
    [SerializeField] private Color lockedBorder    = new Color(0.180f, 0.180f, 0.180f, 1f);

    private int tierIndex;

    public void Bind(int index, int required, int gemReward, bool isClaimed, bool isNext)
    {
        tierIndex = index;
        tierLabel.text = required.ToString();
        gemLabel.text = "\U0001f48e" + gemReward;

        bool isFinalTier = index == 7;

        if (isClaimed)
        {
            backgroundImage.color = claimedBg;
            borderImage.color = claimedBorder;
            stateIcon.text = "\u2713";
            stateIcon.color = new Color(0.545f, 0.765f, 0.290f, 1f);
            gemLabel.color = new Color(0.494f, 0.812f, 1.000f, 1f);
            tierLabel.color = new Color(0.545f, 0.765f, 0.290f, 1f);
            GetComponent<Button>().interactable = false;
        }
        else if (isNext)
        {
            backgroundImage.color = nextBg;
            borderImage.color = nextBorder;
            stateIcon.text = "\u25B6";
            stateIcon.color = new Color(1f, 0.843f, 0f, 1f);
            gemLabel.color = new Color(0.494f, 0.812f, 1.000f, 1f);
            tierLabel.color = Color.white;
            GetComponent<Button>().interactable = true;
        }
        else
        {
            backgroundImage.color = lockedBg;
            borderImage.color = lockedBorder;
            stateIcon.text = isFinalTier ? "\u2605" : "\u2022";
            stateIcon.color = new Color(0.333f, 0.333f, 0.333f, 1f);
            gemLabel.color = new Color(0.267f, 0.400f, 0.533f, 1f);
            tierLabel.color = new Color(0.333f, 0.333f, 0.333f, 1f);
            GetComponent<Button>().interactable = false;
        }

        GetComponent<Button>().onClick.RemoveAllListeners();
        GetComponent<Button>().onClick.AddListener(OnChipClicked);
    }


    private void OnChipClicked()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimMilestone(tierIndex))
        {
            QuestPopup.Instance?.RefreshMilestoneStrip();
        }
    }
}
