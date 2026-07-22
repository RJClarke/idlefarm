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
    [Tooltip("World gap between the bobber and the bottom of the bubble's tail (0.25 = 8px at 32 PPU), " +
             "so the end of the line stays visible while reeling in.")]
    [SerializeField] private float bubbleClearance = 0.25f;
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
        SyncBubble(fm.HasBite, pos + Vector3.up * BubbleLift());
    }

    /// <summary>How far above the bobber the bubble's center must sit so its lowest point (the
    /// speech tail) clears the bobber by <see cref="bubbleClearance"/>. Measured from the live
    /// bubble rect (zero on the creation frame — corrected next frame once layout has run).</summary>
    private float BubbleLift()
    {
        float halfExtent = 0f;
        if (biteIndicator != null)
        {
            var rt = (RectTransform)biteIndicator.transform;
            // Half the box height plus the 12px of speech tail that hangs below the box
            // (16px tail overlapping the 4px border), all in world units.
            halfExtent = (rt.rect.height * 0.5f + 12f) * rt.localScale.y;
        }
        return halfExtent + bubbleClearance;
    }

    // Speech-bubble palette: cream fill with a charcoal outline so the bubble reads against water.
    private static readonly Color BubbleCream    = new Color32(0xFA, 0xF3, 0xE1, 0xF5);
    private static readonly Color BubbleCharcoal = new Color32(0x32, 0x2F, 0x2B, 0xFF);

    private void SyncBubble(bool biting, Vector3 at)
    {
        if (biting && !biteShown)
        {
            HideBubble();
            // Persistent: the bite bubble must stay above the bobber until the fish is reeled in,
            // not fade after a couple seconds like a transient hint. Shows the hooked fish's own
            // icon; falls back to the generic emoji if no icon is wired for the tier.
            var fm = FishingManager.Instance;
            Sprite icon = lake != null && fm != null ? lake.FishIcon(fm.PendingTier) : null;
            biteIndicator = icon != null
                ? WorldHintPopup.CreateIcon(at, icon, BubbleCream, BubbleCharcoal, true)
                : WorldHintPopup.Create(at, "🐟", true);
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
