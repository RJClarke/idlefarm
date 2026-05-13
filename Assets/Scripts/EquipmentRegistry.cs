using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered list of every EquipmentData asset that should appear in the Equipment popup.
/// Display order = inspector order.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentRegistry", menuName = "Farm Game/Equipment Registry", order = 7)]
public class EquipmentRegistry : ScriptableObject
{
    public List<EquipmentData> equipment = new List<EquipmentData>();
}
