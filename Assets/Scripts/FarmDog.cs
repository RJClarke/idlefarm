using UnityEngine;

/// <summary>
/// Farm Dog — chases deer off the fields during a run.
///
/// The patrol/chase machinery lives in <see cref="AnimalDefender"/>; this class supplies the
/// dog-specific parts: it hunts deer only, has a full Run and Bark cycle on its sheet, and its
/// speed/cooldown scale with the Dog research track.
///
/// Animation states (AnimState int on Animator):
///   0-3  = Idle  R/U/L/D
///   4-7  = Walk  R/U/L/D
///   8-11 = Run   R/U/L/D
///  12-15 = Bark  R/U/L/D
/// </summary>
public class FarmDog : AnimalDefender
{
    private const int RUN_OFFSET  = 8;
    private const int BARK_OFFSET = 12;

    private static readonly AnimalThreatType[] Targets = { AnimalThreatType.Deer };

    protected override AnimalThreatType[] TargetTypes => Targets;
    protected override int RunOffset => RUN_OFFSET;
    protected override int VictoryOffset => BARK_OFFSET;
    protected override string LogName => "FarmDog";

    // Split by source so the Almanac can show where each bonus came from.
    public static float ResearchSpeedFactor =>
        ResearchManager.Instance != null ? 1f + ResearchManager.Instance.GetBonus(Research.StatKey.DogEfficiency) : 1f;
    public static float ResearchCooldownDivisor =>
        ResearchManager.Instance != null ? 1f + ResearchManager.Instance.GetBonus(Research.StatKey.DogCooldown) : 1f;

    // Research track × Ranching Lv 25 capstone (1 until maxed).
    protected override float SpeedMultiplier => ResearchSpeedFactor * FarmSkillsManager.DogSpeedMultiplier;

    protected override float CooldownDivisor => ResearchCooldownDivisor;

    protected override void RecordChase(AnimalThreat threat)
    {
        if (RunStats.Instance != null) RunStats.Instance.AddChase(AnimalId, AnimalThreatType.Deer);
    }
}
