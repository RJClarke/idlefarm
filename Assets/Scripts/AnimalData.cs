using UnityEngine;

/// <summary>
/// What an equipped animal does. Flags, because the goose both lays an egg on a timer AND
/// defends during runs. The values are the original enum's ordinals (0/1/2), so every
/// Animal_*.asset serialized before this became a flags enum still deserializes correctly.
/// </summary>
[System.Flags]
public enum AnimalAbilityType
{
    None         = 0,
    PassiveTimer = 1,
    RunDefender  = 2
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

    [Header("Compost (Cow only)")]
    [Tooltip("Base compost generated per real-world minute while equipped. Other animals leave at 0.")]
    public float compostPerMinute = 0f;

    [Header("Visuals")]
    public GameObject visualPrefab;
    public float roamSpeed = 0.6f;
    public Sprite iconSprite;

    [Header("Click SFX")]
    [Tooltip("Sounds played (one picked at random, with slight pitch jitter) when the player taps this " +
             "animal on the farm. Leave empty until clips are ready — tapping still does the little hop.")]
    public AudioClip[] clickSounds;
}
