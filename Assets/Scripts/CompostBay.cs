using UnityEngine;

/// <summary>
/// Compost Bay system. One singleton in the scene listens for Plant.OnPlantDied
/// across all zones. For each zone, if a Compost Bay equipment is assigned via
/// EquipmentManager.AssignEquipment(zoneId, compostBayData), compost is credited.
///
/// Yield per kill = crop.tier × baseConversion, where baseConversion is
/// EquipmentData.baseMoisturePowerPerSecond × (1 + Compost Bay Conversion Efficiency research bonus).
/// The waterPower channel on the Bay's EquipmentData is hijacked for this multiplier.
/// </summary>
public class CompostBay : MonoBehaviour
{
    public static CompostBay Instance { get; private set; }

    [Tooltip("Drag the Compost Bay EquipmentData here so the manager can match assignments.")]
    [SerializeField] private EquipmentData compostBayData;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => Plant.OnPlantDied += HandlePlantDied;
    private void OnDisable() => Plant.OnPlantDied -= HandlePlantDied;

    private void HandlePlantDied(int zoneID, int cropTier)
    {
        if (compostBayData == null || EquipmentManager.Instance == null || CurrencyManager.Instance == null) return;

        EquipmentData assigned = EquipmentManager.Instance.GetAssignment(zoneID);
        if (assigned != compostBayData) return; // bay not equipped on this zone

        float conversion = EquipmentManager.Instance.GetEffectiveWaterPower(compostBayData);
        int yield = Mathf.Max(1, Mathf.RoundToInt(cropTier * conversion));
        CurrencyManager.Instance.AddCompost(yield);
    }
}
