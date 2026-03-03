using UnityEngine;
using System.Collections;

/// <summary>
/// Deer Threat
///
/// Behavior:
///   1. Runs in from left or right screen edge toward the assigned zone
///   2. Walks to each target plant once inside the zone
///   3. Eats plants with bites over time
///   4. Idles briefly when searching for the next plant
///   5. Runs off the opposite screen edge when full or no targets remain
///
/// Animation states (AnimState int on Animator):
///   0 = Walk  — moving toward a known plant target
///   1 = Run   — entering or exiting the farm
///   2 = Eat   — biting a plant
///   3 = Idle  — searching for the next plant
/// </summary>
public class DeerThreat : AnimalThreat
{
    private bool enteredFromLeft;

    // ─────────────────────────────────────────────────────────────────────
    // Animation Hooks
    // ─────────────────────────────────────────────────────────────────────

    protected override void OnEnteringFarm()      => SetAnimation(1); // Run — charging in
    protected override void OnMovingToPlant()     => SetAnimation(0); // Walk — approaching target
    protected override void OnSearchingForPlant() => SetAnimation(3); // Idle — looking around
    protected override void OnEatingPlant()       => SetAnimation(2); // Eat — nom nom
    protected override void OnExitingFarm()       => SetAnimation(1); // Run — fleeing full

    // ─────────────────────────────────────────────────────────────────────
    // Entry / Exit
    // ─────────────────────────────────────────────────────────────────────

    protected override IEnumerator EnterFarm()
    {
        if (FarmGrid.Instance == null) yield break;

        Vector3 zoneCenter = FarmGrid.Instance.GetZoneCenter(assignedZoneId);

        enteredFromLeft = Random.value < 0.5f;

        float edgeX      = enteredFromLeft ? ScreenLeftX() : ScreenRightX();
        float jitterY    = Random.Range(-0.5f, 0.5f);
        Vector3 entryPos = new Vector3(edgeX, zoneCenter.y + jitterY, 0f);

        transform.position = entryPos;

        yield return StartCoroutine(MoveTo(zoneCenter, data.moveSpeed));
    }

    protected override IEnumerator ExitFarm()
    {
        float exitX     = enteredFromLeft ? ScreenRightX() : ScreenLeftX();
        Vector3 exitPos = new Vector3(exitX, transform.position.y, 0f);
        yield return StartCoroutine(MoveTo(exitPos, data.moveSpeed * 1.2f));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Target Selection
    // ─────────────────────────────────────────────────────────────────────

    protected override Plant FindFirstTarget()
    {
        return FindBestPlantInRadius(transform.position, data.grazingRadius);
    }

    protected override Plant FindNextTarget(Vector3 currentPosition)
    {
        return FindBestPlantInRadius(currentPosition, data.grazingRadius);
    }
}