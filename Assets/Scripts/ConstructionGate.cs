using UnityEngine;

/// <summary>
/// Hides this GameObject's renderer/collider until <see cref="BuildingState.IsBuilt"/> returns true
/// for <see cref="buildingKey"/>. Stays alive so it can react when the building is constructed.
/// </summary>
[DisallowMultipleComponent]
public class ConstructionGate : MonoBehaviour
{
    [Tooltip("BuildingState key — match this against the carpenter's purchase target.")]
    [SerializeField] private string buildingKey = BuildingState.GreenhouseKey;

    private void OnEnable()
    {
        BuildingState.OnBuildingBuilt += OnBuilt;
        Apply();
    }

    private void OnDisable()
    {
        BuildingState.OnBuildingBuilt -= OnBuilt;
    }

    private void OnBuilt(string key)
    {
        if (key == buildingKey) Apply();
    }

    private void Apply()
    {
        bool built = BuildingState.IsBuilt(buildingKey);
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++) sprites[i].enabled = built;
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = built;
    }
}
