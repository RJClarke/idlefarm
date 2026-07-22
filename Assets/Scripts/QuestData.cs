using UnityEngine;

public enum QuestObjectiveType
{
    HarvestCrops,
    PlantSeeds,
    WaterPlants,
    RepelDeer,
    RepelCrows,
    GatherEggs,
    GatherGems,
    ChopTrees,   // fell a tree in the Woods (counts once per tree felled)
    CollectWood  // wood gathered while chopping (counts by amount, not by swing)
    // NOTE: CatchFish is intentionally not here yet — add it alongside the Fishing system so a
    // fishing quest can actually progress (no event source exists to advance it today).
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
