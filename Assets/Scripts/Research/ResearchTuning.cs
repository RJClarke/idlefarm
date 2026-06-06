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

        [Header("Tick Cadence")]
        [Tooltip("How often ResearchManager polls real-time elapsed and applies level-ups.")]
        public float tickIntervalSecs = 1.0f;

        [Header("Branches (display order)")]
        public string[] branchOrder = new[] { "soil", "helper", "plant", "animals", "equipment", "weather", "meta" };
    }
}
