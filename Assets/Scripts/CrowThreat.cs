using UnityEngine;
using System.Collections;

/// <summary>
/// Crow Threat
///
/// Behavior:
///   1. Flies in from a random 360° direction toward the assigned zone
///   2. Locks to that zone — NEVER switches zones mid-lifecycle
///   3. Hops to the nearest valid plant (Seeds or Harvestable only) in the zone
///   4. Pecks it 2-4 times (10 HP each, 1-2 sec apart)
///   5. Hops to the nearest next valid plant in the zone
///   6. If no valid plants remain in zone → flies off immediately (hunger or not)
///   7. When hunger = 0 → flies off in a random 360° direction
///
/// Stage multipliers (configure in AnimalThreatData):
///   Seed        → 2.0  (priority)
///   Sprout      → 0.0  (ignored)
///   Sapling     → 0.0  (ignored)
///   Harvestable → 1.5  (priority)
///
/// Recommended AnimalThreatData settings:
///   moveSpeed      = 5.0   (faster than deer — it hops)
///   grazingRadius  = 10.0  (whole zone — crow searches entire assigned zone)
///   minBites       = 2, maxBites = 4
///   minDamage      = 10, maxDamage = 10  (fixed 10 HP per peck)
///   biteInterval   = 1.5s
///   placeholderColor = dark grey / near-black
/// </summary>
public class CrowThreat : AnimalThreat
{
    // Entry angle — reused for exit (flies back the way it came, roughly)
    private float entryAngleDeg;

    // ─────────────────────────────────────────────────────────────────────
    // Entry / Exit
    // ─────────────────────────────────────────────────────────────────────

    protected override IEnumerator EnterFarm()
    {
        if (FarmGrid.Instance == null) yield break;

        Vector3 zoneCenter = FarmGrid.Instance.GetZoneCenter(assignedZoneId);

        // Random 360° entry direction
        entryAngleDeg = Random.Range(0f, 360f);

        // Spawn at edge in that direction
        transform.position = GetScreenEdgePoint(entryAngleDeg);

        // Fly directly to zone center
        yield return StartCoroutine(MoveTo(zoneCenter, data.moveSpeed));
    }

    protected override IEnumerator ExitFarm()
    {
        // Fly back out roughly the same direction it came from (±30° randomness)
        float exitAngle = entryAngleDeg + 180f + Random.Range(-30f, 30f);
        Vector3 exitPos = GetScreenEdgePoint(exitAngle);
        yield return StartCoroutine(MoveTo(exitPos, data.moveSpeed * 1.3f));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Target Selection — zone-locked, nearest-first
    // ─────────────────────────────────────────────────────────────────────

    protected override Plant FindFirstTarget()
    {
        // Large radius = whole zone; crow searches everywhere in its assigned zone
        return FindNearestValidPlantInZone(transform.position);
    }

    protected override Plant FindNextTarget(Vector3 currentPosition)
    {
        return FindNearestValidPlantInZone(currentPosition);
    }

    /// <summary>
    /// Unlike deer (which uses priority scoring), crows hop to the NEAREST
    /// valid plant in their locked zone. Uses distance as primary sort,
    /// with the stage filter still applied (Seeds/Harvestable only via multipliers).
    /// </summary>
    private Plant FindNearestValidPlantInZone(Vector3 origin)
    {
        if (FarmGrid.Instance == null) return null;

        var tiles = FarmGrid.Instance.GetOccupiedTilesInZone(assignedZoneId);
        Plant nearest = null;
        float nearestDist = float.MaxValue;

        foreach (SoilTile tile in tiles)
        {
            if (tile.CurrentPlant == null) continue;
            Plant plant = tile.CurrentPlant.GetComponent<Plant>();
            if (plant == null) continue;

            // Must be a targetable stage (multiplier > 0 = Seeds or Harvestable)
            if (!data.CanTargetStage(plant.CurrentStage)) continue;

            float dist = Vector3.Distance(origin, tile.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest     = plant;
            }
        }

        return nearest;
    }
}