using UnityEngine;

public enum QuestObjectiveType
{
    HarvestCrops,
    PlantSeeds,
    WaterPlants,
    RepelDeer,
    RepelCrows,
    GatherEggs,
    GatherGems
}

[CreateAssetMenu(menuName = "Farm Game/Quest Data", order = 8)]
public class QuestData : ScriptableObject
{
    public string questID;
    public string displayName;
    public string description;
    public QuestObjectiveType objectiveType;
    public int targetCount;
    public int coinReward;
    [Tooltip("UpgradeManager permanent upgrade ID required. Empty = always eligible.")]
    public string requiredUnlockID;
    [Tooltip("AnimalManager animal ID required. Empty = no animal required.")]
    public string requiredAnimalID;
}
