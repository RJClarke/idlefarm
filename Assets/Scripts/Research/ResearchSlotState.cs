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
        // When the boost window opened. The multiplier only credits time from here forward —
        // without it the bonus applied retroactively to everything since startUtcTicks, so buying
        // a 4x mid-research made the remaining time collapse instead of just ticking down faster.
        // 0 in saves written before this existed; falls back to the old anchor (see ComputeElapsedSeconds).
        public long boostStartUtcTicks;

        // Auto-buy: when a boost expires, automatically purchase another of the same kind
        // if the player has enough compost. 0/cleared values disable auto-buy.
        public float autoBuyMultiplier;
        public float autoBuyDurationSecs;
        public int   autoBuyCost;

        // Auto-repeat ("Auto"): when a level finishes, immediately buy and start the NEXT level of
        // the same research instead of idling the slot. Sticky per-slot — survives cancel/reassign
        // so the player sets it once. Off by default: finishing a level idles the slot.
        public bool autoRepeat;

        public bool HasActiveBoost(System.DateTime nowUtc) =>
            boostMultiplier > 1.0f && boostExpiresUtcTicks > nowUtc.Ticks;

        public bool HasAutoBuy => autoBuyMultiplier > 1.0f && autoBuyDurationSecs > 0f && autoBuyCost > 0;

        public bool IsIdle => string.IsNullOrEmpty(activeResearchID) || startUtcTicks == 0;
    }
}
