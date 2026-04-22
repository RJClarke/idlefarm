using UnityEngine;

public enum AnimalAbilityType
{
    None,
    PassiveTimer,
    RunDefender
}

[CreateAssetMenu(fileName = "New Animal", menuName = "Farm Game/Animal Data", order = 7)]
public class AnimalData : ScriptableObject
{
    [Header("Identity")]
    public string animalID;
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public string animalEmoji;
    public int sortOrder;

    [Header("Cost")]
    public int gemCost;

    [Header("Ability")]
    public AnimalAbilityType abilityType;

    [Tooltip("For PassiveTimer: real-time cooldown in minutes")]
    public float cooldownMinutes = 20f;

    [Tooltip("For PassiveTimer (coin animals): coins rewarded per claim")]
    public int rewardCoins = 30;

    [Tooltip("For PassiveTimer (gem animals): gems rewarded per claim. Set > 0 to make this a gem animal instead of a coin animal.")]
    public int rewardGems = 0;

    [Header("Visuals")]
    public GameObject visualPrefab;
    public float roamSpeed = 0.6f;
    public Sprite iconSprite;
}
