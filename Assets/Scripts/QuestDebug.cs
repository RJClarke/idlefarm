#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public class QuestDebug : MonoBehaviour
{
    private void OnGUI()
    {
        if (QuestManager.Instance == null) return;

        GUILayout.BeginArea(new Rect(10, 180, 230, 250));
        GUI.Box(new Rect(0, 0, 230, 250), "Quest Debug");
        GUILayout.Space(22);

        if (GUILayout.Button("Spawn Quest(s)"))
        {
            QuestManager.Instance.DebugForceDrop();
            QuestPopup.Instance?.RefreshAll();
        }

        if (GUILayout.Button("Complete All Quests"))
        {
            QuestManager.Instance.DebugCompleteAll();
            QuestPopup.Instance?.RefreshAll();
        }

        if (GUILayout.Button("Claim All Completed"))
        {
            QuestManager.Instance.DebugClaimAll();
            QuestPopup.Instance?.RefreshAll();
        }

        if (GUILayout.Button("Max Week Progress"))
        {
            QuestManager.Instance.DebugClaimAllMilestones();
            QuestPopup.Instance?.RefreshAll();
        }

        if (GUILayout.Button("Reset All Progress"))
        {
            QuestManager.Instance.DebugResetAllProgress();
            QuestPopup.Instance?.RefreshAll();
        }

        int week = QuestManager.Instance.QuestsCompletedThisWeek;
        int active = QuestManager.Instance.ActiveQuestCount;
        GUILayout.Label($"Week: {week}/40   Active: {active}/10");

        GUILayout.EndArea();
    }
}
#endif
