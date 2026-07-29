using UnityEngine;

/// <summary>
/// Farm Goose — the belligerent one. Unlike the dog she goes after BOTH deer and crows, which is
/// what makes her worth the price; the trade is that she is a step slower off the mark and takes
/// longer to work herself up to the next one, so per-threat she is less effective than the dog.
///
/// Her spritesheet only has Idle and Walk (no run or bark cycle), so the chase reuses the walk
/// clip played fast — a goose in a hurry is meant to look ridiculous, not graceful.
///
/// Defaults here are deliberately weaker than FarmDogVisual.prefab's tuned values
/// (chaseSpeed 5, chaseCooldown 12). The prefab's serialized values win, so keep the two in step
/// if you retune the dog.
/// </summary>
public class FarmGoose : AnimalDefender
{
    private static readonly AnimalThreatType[] Targets =
    {
        AnimalThreatType.Deer,
        AnimalThreatType.Crow
    };

    protected override AnimalThreatType[] TargetTypes => Targets;
    protected override string LogName => "FarmGoose";

    // No run cycle on the sheet — sprint on the walk clip, just played faster.
    protected override int RunOffset => WALK_OFFSET;
    protected override float ChaseAnimSpeed => 2.2f;

    // No bark cycle either; she just stands there looking pleased with herself.
    protected override int VictoryOffset => IDLE_OFFSET;

    protected override void RecordChase(AnimalThreat threat)
    {
        if (RunStats.Instance == null) return;
        if (threat != null && threat.ThreatType == AnimalThreatType.Crow)
            RunStats.Instance.AddCrowChasedByAnimal();
        else
            RunStats.Instance.AddDeerChasedByAnimal();
    }

    private void Reset()
    {
        // Slower and longer-cooldown than the dog's tuned 5 / 12.
        chaseSpeed = 4f;
        chaseCooldown = 18f;
        roamSpeed = 1.4f;
        roamRadius = 5f;
        victoryDuration = 1.2f;
    }
}
