using UnityEngine;

/// <summary>Map nav button for the Lake (fishing area, right of the farm). Stays available during a run; hidden only at the Market.</summary>
public class LakeNavButton : MapNavButton
{
    protected override CameraPanController.Location Target => CameraPanController.Location.Lake;

    protected override bool ShouldHide(CameraPanController.Location current)
        => current == CameraPanController.Location.Market;
}
