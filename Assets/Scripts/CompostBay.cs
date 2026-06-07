using UnityEngine;

/// <summary>
/// Per-zone Compost Bay. Subscribes to Plant.OnPlantDied and credits compost
/// for dying plants in its assigned zone. Yield per kill = crop.tier × baseConversion
/// (read from EquipmentData via EquipmentManager.GetEffectiveWaterPower, which folds
/// in the Compost Bay Conversion Efficiency research bonus).
/// </summary>
public class CompostBay : MonoBehaviour
{
    [SerializeField] private int zoneID = 0;
    [SerializeField] private EquipmentData data;
    public int ZoneID => zoneID;

    public void Initialize(int zone, EquipmentData eq)
    {
        zoneID = zone;
        data = eq;
    }

    private void OnEnable()  => Plant.OnPlantDied += HandlePlantDied;
    private void OnDisable() => Plant.OnPlantDied -= HandlePlantDied;

    private void HandlePlantDied(int diedZone, int cropTier)
    {
        if (diedZone != zoneID) return;
        if (data == null || EquipmentManager.Instance == null || CurrencyManager.Instance == null) return;

        // GetEffectiveWaterPower returns baseMoisturePowerPerSecond × (1 + research) on Compost Bay,
        // which we hijack as the conversion multiplier.
        float conversion = EquipmentManager.Instance.GetEffectiveWaterPower(data);
        int yield = Mathf.Max(1, Mathf.RoundToInt(cropTier * conversion));
        CurrencyManager.Instance.AddCompost(yield);
    }
}
