using UnityEngine;

/// <summary>Map nav button for the Woods (bottom-right chop area). Stays available during a run; hidden only at the Market.</summary>
public class WoodsNavButton : MapNavButton
{
    protected override CameraPanController.Location Target => CameraPanController.Location.Woods;

    protected override bool ShouldHide(CameraPanController.Location current)
        => current == CameraPanController.Location.Market;
}
