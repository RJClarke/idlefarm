using UnityEngine;

namespace Research
{
    [CreateAssetMenu(menuName = "Farm Game/Research Data", fileName = "Research_New", order = 9)]
    public class ResearchData : ScriptableObject
    {
        [Header("Identity")]
        public string researchID;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public string branchID; // soil | helper | plant | animals | equipment | weather | meta

        [Header("Tier")]
        public ResearchTier tier = ResearchTier.Tier100Standard;

        [Header("Bonus")]
        [Tooltip("Empty for binary unlocks.")]
        public string targetStatKey;
        [Tooltip("Additive per level. e.g. 0.005 means +0.5%/lvl; for Game Speed use 0.9.")]
        public float bonusPerLevel = 0.005f;

        [Header("Scaling")]
        public float baseCostCoins = 50f;
        public float baseDurationSecs = 120f; // 2 minutes
        [Tooltip("Per-research multiplier applied on top of the polynomial base. 1.0 = neutral.")]
        public float timeDifficulty = 1.0f;
        [Tooltip("Per-research multiplier applied on top of the polynomial base. 1.0 = neutral.")]
        public float costDifficulty = 1.0f;

        [Header("Prerequisites / Visibility")]
        public string prerequisiteResearchID; // empty if none
        public string requiredUnlockID;       // empty if none — checked via UpgradeManager
        public string requiredAnimalID;       // empty if none — checked via AnimalManager

        [Header("Binary overrides (when tier == Binary)")]
        public int binaryFixedCost;            // single up-front coin cost
        public float binaryFixedDurationSecs;  // single duration

        [Header("On Complete (binary only)")]
        public int unlocksSlotIndex = -1;     // -1 = not a slot unlock; 2 = Slot 3, 3 = Slot 4
        public string unlocksFeatureID;       // e.g. FeatureFlag.OfflineProgress

        public int MaxLevel => tier.MaxLevel();
        public bool IsBinary => tier == ResearchTier.Binary;
    }
}
