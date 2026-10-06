using System;
using UnityEngine;

public enum RewardKind { None, Coins, Gems, Compost }

/// <summary>What the letter's call-to-action button navigates to. Reserve room for
/// future targets (coach-marks are out of scope for now).</summary>
/// Append only — values are serialized by index in LetterCatalog.asset.
public enum CtaKind { None, OpenEquipment, OpenResearch, OpenShop, OpenFarmUpgrades, OpenCarpenter, OpenBarn, OpenAnimals, OpenFieldPicker, OpenMarket, OpenTownRequests, OpenPlantsShop }

/// <summary>Authored content for one letter. State (read/claimed) lives in InboxEntry,
/// not here. Trigger fields are optional: a letter with neither trigger is delivered
/// imperatively (e.g. the welcome letter on first-run naming).</summary>
[Serializable]
public class LetterDef
{
    public string id;

    [Header("Trigger (optional)")]
    public string triggerFeatureFlag; // matches ResearchManager.OnFeatureFlagUnlocked
    public string triggerAnimalId;    // matches AnimalManager.OnAnimalUnlocked
    [Tooltip("Game event id(s) from NarrativeDirector.Raise, e.g. \"tree_felled\" or \"run_ended:3|axe_bought\" (any one fires it).")]
    public string triggerEvent;
    [Tooltip("Onboarding letters: only delivered to farms named after onboarding shipped, so older saves don't get a burst of 'welcome to X' mail.")]
    public bool newPlayersOnly;

    [Header("Content")]
    public string senderName;
    public Sprite senderPortrait;
    public string subject;
    [TextArea(3, 8)] public string body; // may contain {farmName}

    [Header("Reward (optional)")]
    public RewardKind rewardKind = RewardKind.None;
    public int rewardAmount;
    [Tooltip("Crop name (e.g. \"Radish\") of a seed packet enclosed with the letter. Shown in the Enclosed list; claiming grants the crop if the player doesn't own it yet.")]
    public string giftSeed;

    public bool HasCurrencyReward => rewardKind != RewardKind.None && rewardAmount > 0;
    public bool HasGiftSeed => !string.IsNullOrEmpty(giftSeed);
    /// <summary>True when the letter encloses anything to claim.</summary>
    public bool HasReward => HasCurrencyReward || HasGiftSeed;

    [Header("Call to action (optional)")]
    public CtaKind ctaKind = CtaKind.None;
    public string ctaArg;
}

/// <summary>A recurring character who sends mail. Content-only: the reference doc
/// (Farm Game > Narrative > Write Copy Reference) groups letters by displayName.</summary>
[Serializable]
public class CastMember
{
    public string id;
    public string displayName;          // must match LetterDef.senderName
    public string role;                 // one line: who they are in town
    [TextArea(2, 6)] public string voice; // how they talk — for keeping new copy in character
    public Sprite portrait;
}

/// <summary>A one-time how-to tip shown by the tutorial overlay. The trigger lives in code
/// (OnboardingTutorials); `when` just documents it for the copy reference.</summary>
[Serializable]
public class TipDef
{
    public string id;
    [TextArea(2, 6)] public string text;
    public string when;
}
