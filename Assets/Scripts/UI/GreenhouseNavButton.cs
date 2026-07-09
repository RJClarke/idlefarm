using UnityEngine;

/// <summary>Map nav button for the Greenhouse. Hidden until the Greenhouse is built, and while at the Market.</summary>
public class GreenhouseNavButton : MapNavButton
{
    protected override CameraPanController.Location Target => CameraPanController.Location.Greenhouse;

    protected override bool ShouldHide(CameraPanController.Location current)
        => !BuildingState.IsBuilt(BuildingState.GreenhouseKey)
        || current == CameraPanController.Location.Market;

    protected override void SubscribeExtra()   => BuildingState.OnBuildingBuilt += OnBuildingBuilt;
    protected override void UnsubscribeExtra() => BuildingState.OnBuildingBuilt -= OnBuildingBuilt;
    private void OnBuildingBuilt(string _) => RefreshVisibility();
}
