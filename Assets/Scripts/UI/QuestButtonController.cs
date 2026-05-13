using UnityEngine;
using UnityEngine.UI;

public class QuestButtonController : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject notificationDot;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        if (button != null) button.onClick.AddListener(OpenPopup);
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += UpdateDot;
            QuestManager.Instance.OnQuestsDropped  += UpdateDot;
        }
        UpdateDot();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OpenPopup);
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= UpdateDot;
            QuestManager.Instance.OnQuestsDropped  -= UpdateDot;
        }
    }

    private void OpenPopup()
    {
        QuestPopupUITK.Instance?.Open();
    }

    private void UpdateDot()
    {
        if (notificationDot == null) return;
        bool show = QuestManager.Instance != null &&
                    (QuestManager.Instance.HasUnclaimedCompleted || QuestManager.Instance.HasNewDrops);
        notificationDot.SetActive(show);
    }
}
