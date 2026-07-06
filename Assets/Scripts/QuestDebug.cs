#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public class QuestDebug : MonoBehaviour
{
    // Start collapsed so the game view isn't blocked.
    private bool isExpanded = false;

    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private bool stylesInitialized = false;

    private const float WIDTH = 280f;
    private const float HEADER_HEIGHT = 44f;
    private const float BUTTON_HEIGHT = 42f;
    private const float SPACING = 4f;
    private const float LABEL_HEIGHT = 28f;
    private const float PADDING = 6f;

    private void EnsureStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (QuestManager.Instance == null) return;
        EnsureStyles();

        const int BUTTON_COUNT = 6;
        float expandedHeight = HEADER_HEIGHT
            + SPACING
            + BUTTON_COUNT * (BUTTON_HEIGHT + SPACING)
            + LABEL_HEIGHT
            + PADDING * 2;
        float height = isExpanded ? expandedHeight : HEADER_HEIGHT;

        // Sit below the top-left stack (Missions / Run Stats / Daily). Proportional to screen
        // height so it stays below the daily reward button across resolutions.
        GUILayout.BeginArea(new Rect(10, Screen.height * 0.17f, WIDTH, height));

        string toggleLabel = isExpanded ? "▲ Quest Debug" : "▼ Quest Debug";
        if (GUILayout.Button(toggleLabel, headerStyle, GUILayout.Height(HEADER_HEIGHT)))
        {
            isExpanded = !isExpanded;
        }

        if (isExpanded)
        {
            GUILayout.Space(SPACING);

            if (GUILayout.Button("Spawn Quest(s)", buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
                QuestManager.Instance.DebugForceDrop();

            if (GUILayout.Button("Complete All Quests", buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
                QuestManager.Instance.DebugCompleteAll();

            if (GUILayout.Button("Claim All Completed", buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
                QuestManager.Instance.DebugClaimAll();

            if (GUILayout.Button("Max Week Progress", buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
                QuestManager.Instance.DebugClaimAllMilestones();

            if (GUILayout.Button("Reset All Progress", buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
                QuestManager.Instance.DebugResetAllProgress();

            if (GUILayout.Button("Reset Daily", buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
            {
                if (DailyRewardManager.Instance != null)
                    DailyRewardManager.Instance.DebugResetDaily();
            }

            int week = QuestManager.Instance.QuestsCompletedThisWeek;
            int active = QuestManager.Instance.ActiveQuestCount;
            GUILayout.Label($"Week: {week}/40    Active: {active}/10", labelStyle, GUILayout.Height(LABEL_HEIGHT));
        }

        GUILayout.EndArea();
    }
}
#endif
