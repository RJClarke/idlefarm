using UnityEngine;

/// <summary>
/// Renders the in-flight fishing line: a bobber in the water, a line from the pole to it, and the
/// bite bubble (fish icon) above it. Pure view — reads FishingManager state and LakeNode geometry
/// each frame. The bobber "agitates" (spins) while inside a whirlpool as the player's cue.
/// Replaces LakeNode's old fixed-offset bite indicator.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class FishingLineVisual : MonoBehaviour
{
    [SerializeField] private LakeNode lake;              // geometry source
    [SerializeField] private SpriteRenderer bobber;      // bobber.png
    [SerializeField] private LineRenderer line;          // pole → bobber
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("Degrees/second the bobber spins while inside a whirlpool.")]
    [SerializeField] private float agitationSpin = 540f;

    private WorldHintPopup biteIndicator;
    private bool biteShown;
    private bool agitated;
    private float spin;

    public void SetAgitated(bool on) => agitated = on;

    private void Reset() => line = GetComponent<LineRenderer>();

    private void Update()
    {
        var fm = FishingManager.Instance;
        bool cast = fm != null && (fm.State == FishingManager.CastState.Waiting || fm.State == FishingManager.CastState.Bite);

        if (bobber != null) bobber.enabled = cast;
        if (line != null) line.enabled = cast;

        if (!cast) { HideBubble(); return; }

        Vector3 pos = lake != null ? lake.CurrentBobberWorldPos() : transform.position;
        if (bobber != null)
        {
            bobber.transform.position = pos;
            if (agitated) { spin += agitationSpin * Time.deltaTime; bobber.transform.rotation = Quaternion.Euler(0f, 0f, spin); }
            else bobber.transform.rotation = Quaternion.identity;
        }
        if (line != null && lake != null)
        {
            line.positionCount = 2;
            line.SetPosition(0, lake.CastOrigin);
            line.SetPosition(1, pos);
        }
        SyncBubble(fm.HasBite, pos + bubbleOffset);
    }

    private void SyncBubble(bool biting, Vector3 at)
    {
        if (biting && !biteShown)
        {
            HideBubble();
            biteIndicator = WorldHintPopup.Create(at, "🐟");
            biteShown = true;
        }
        else if (biting && biteShown && biteIndicator != null)
        {
            biteIndicator.transform.position = at; // follow the bobber as it reels
        }
        else if (!biting && biteShown) HideBubble();
    }

    private void HideBubble()
    {
        if (biteIndicator != null) Destroy(biteIndicator.gameObject);
        biteIndicator = null; biteShown = false;
    }
}
