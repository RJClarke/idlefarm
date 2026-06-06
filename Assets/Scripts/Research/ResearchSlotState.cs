using System;

namespace Research
{
    /// <summary>
    /// Per-slot active research state. Persisted in GameData.json.
    /// startUtcTicks = 0 means slot has no active research (idle).
    /// </summary>
    [Serializable]
    public class ResearchSlotState
    {
        public int slotIndex;
        public string activeResearchID = "";
        public int currentLevel;          // 0..MaxLevel for the active research; 0 for idle
        public long startUtcTicks;        // ticks of UTC at the moment the current level started; 0 = idle

        // Boost (Plan 2 — included now so save format is stable across both plans)
        public long boostExpiresUtcTicks; // 0 = no boost active
        public float boostMultiplier = 1.0f;

        public bool IsIdle => string.IsNullOrEmpty(activeResearchID) || startUtcTicks == 0;
    }
}
