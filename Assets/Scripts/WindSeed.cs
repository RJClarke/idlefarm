using UnityEngine;

/// <summary>
/// Gives a static wind-swaying sprite (e.g. a tree) its own random _SwaySeed via a
/// MaterialPropertyBlock, so each one leans on its own schedule instead of in unison.
/// Crops set their seed themselves (PlantVisuals); this is for scene-placed sprites.
///
/// Also drives per-renderer sway shaping so trees bend further and lean slower than crops
/// (the crops keep the material defaults of 1×). Tune per instance in the Inspector.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class WindSeed : MonoBehaviour
{
    private static readonly int SeedID = Shader.PropertyToID("_SwaySeed");
    private static readonly int StrengthMulID = Shader.PropertyToID("_SwayStrengthMul");
    private static readonly int PeriodMulID = Shader.PropertyToID("_SwayPeriodMul");

    [SerializeField, Range(0f, 1f)] private float seed = -1f;

    [Tooltip("How much further this sprite bends than a crop (1 = same as crops). Trees: ~2.5–3.5.")]
    [SerializeField, Range(0.5f, 6f)] private float swayStrengthMul = 3f;

    [Tooltip("How much slower this sprite leans than a crop (1 = same cadence). >1 = slower, lazier.")]
    [SerializeField, Range(0.5f, 3f)] private float swayPeriodMul = 1.5f;

    private MaterialPropertyBlock _mpb;

    private void OnEnable() => Apply();

    private void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    private void Apply()
    {
        if (seed < 0f) seed = Random.value;
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(SeedID, seed);
        _mpb.SetFloat(StrengthMulID, swayStrengthMul);
        _mpb.SetFloat(PeriodMulID, swayPeriodMul);
        sr.SetPropertyBlock(_mpb);
    }
}
