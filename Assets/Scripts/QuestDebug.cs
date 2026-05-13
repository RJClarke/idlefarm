#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public class QuestDebug : MonoBehaviour
{
    private void OnGUI()
    {
        if (QuestManager.Instance == null) return;

        GUILayout.BeginArea(new Rect(10, 180, 230, 290));
        GUI.Box(new Rect(0, 0, 230, 290), "Quest Debug");
        GUILayout.Space(22);

        if (GUILayout.Button("Spawn Quest(s)"))
        {
            QuestManager.Instance.DebugForceDrop();
        }

        if (GUILayout.Button("Complete All Quests"))
        {
            QuestManager.Instance.DebugCompleteAll();
        }

        if (GUILayout.Button("Claim All Completed"))
        {
            QuestManager.Instance.DebugClaimAll();
        }

        if (GUILayout.Button("Max Week Progress"))
        {
            QuestManager.Instance.DebugClaimAllMilestones();
        }

        if (GUILayout.Button("Reset All Progress"))
        {
            QuestManager.Instance.DebugResetAllProgress();
        }

        if (GUILayout.Button("Reset Daily"))
        {
            if (DailyRewardManager.Instance != null)
                DailyRewardManager.Instance.DebugResetDaily();
        }

        int week = QuestManager.Instance.QuestsCompletedThisWeek;
        int active = QuestManager.Instance.ActiveQuestCount;
        GUILayout.Label($"Week: {week}/40   Active: {active}/10");

        GUILayout.EndArea();
    }
}
#endif
