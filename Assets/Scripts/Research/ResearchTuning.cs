using UnityEngine;

namespace Research
{
    [CreateAssetMenu(menuName = "Farm Game/Research Tuning", fileName = "ResearchTuning", order = 9)]
    public class ResearchTuning : ScriptableObject
    {
        [Header("Polynomial Exponents")]
        [Tooltip("duration(L) = baseDurationSecs × timeDifficulty × L^p_time")]
        public float pTime = 2.16f;
        [Tooltip("cost(L) = baseCostCoins × costDifficulty × L^p_cost")]
        public float pCost = 2.00f;

        [Header("Global Economy Multipliers")]
        [Tooltip("Scales EVERY non-binary research cost. 1.0 = neutral; 0.25 = 75% cheaper. One knob for sweeping the whole cost curve.")]
        public float costMultiplier = 0.25f;
        [Tooltip("Scales EVERY non-binary research time. 1.0 = neutral; 0.5 = 50% faster. One knob for sweeping the whole time curve.")]
        public float timeMultiplier = 0.5f;

        [Header("Tick Cadence")]
        [Tooltip("How often ResearchManager polls real-time elapsed and applies level-ups.")]
        public float tickIntervalSecs = 1.0f;

        [Header("Branches (display order)")]
        public string[] branchOrder = new[] { "soil", "helper", "plant", "animals", "equipment", "weather", "meta" };
    }
}
