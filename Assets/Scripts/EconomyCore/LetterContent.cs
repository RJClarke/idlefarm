using System;
using UnityEngine;

public enum RewardKind { None, Coins, Gems, Compost }

/// <summary>What the letter's call-to-action button navigates to. Reserve room for
/// future targets (coach-marks are out of scope for now).</summary>
public enum CtaKind { None, OpenEquipment, OpenResearch, OpenShop }

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

    [Header("Content")]
    public string senderName;
    public Sprite senderPortrait;
    public string subject;
    [TextArea(3, 8)] public string body; // may contain {farmName}

    [Header("Reward (optional)")]
    public RewardKind rewardKind = RewardKind.None;
    public int rewardAmount;

    [Header("Call to action (optional)")]
    public CtaKind ctaKind = CtaKind.None;
    public string ctaArg;
}
