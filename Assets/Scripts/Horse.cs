using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// During-run behaviour for the Horse: he pulls the plough. When untilled soil appears he notices
/// it after a beat, lines up at the near end of the nearest row that needs work, and plows that row
/// end to end — tilling every untilled tile he crosses — then turns into the next row, snaking back
/// and forth until nothing is left (route = <see cref="PlowPlanner"/>). With nothing to plow he hands
/// movement back to <see cref="AnimalVisual"/> and wanders.
///
/// Only zones helpers would work (a seed assigned and affordable) are plowed, so he never churns a
/// field that would just go fallow again. Speed scales with the "Horse: Plow Speed" research.
/// </summary>
public class Horse : MonoBehaviour
{
    [Header("Plowing")]
    [Tooltip("World units per second while plowing a row (tiles are 1 unit wide).")]
    [SerializeField] private float plowSpeed = 1.2f;
    [Tooltip("World units per second while walking over to line up with a row.")]
    [SerializeField] private float travelSpeed = 2.2f;
    [Tooltip("Seconds between untilled soil appearing and the horse heading for it.")]
    [SerializeField] private float noticeDelay = 1f;
    [Tooltip("Where the horse's transform sits relative to the tile centre he's plowing, so his " +
             "hooves (not his middle) run along the row.")]
    [SerializeField] private Vector3 hitchOffset = new Vector3(0f, 0.9f, 0f);
    [Tooltip("How often (seconds) an idle horse looks for untilled soil.")]
    [SerializeField] private float scanInterval = 0.25f;

    // Almanac read-outs.
    public float PlowSpeed => plowSpeed;
    public float TravelSpeed => travelSpeed;

    private static float SpeedMultiplier =>
        ResearchManager.Instance != null ? 1f + ResearchManager.Instance.GetBonus(Research.StatKey.HorsePlowSpeed) : 1f;

    private Coroutine loop;
    private AnimalVisual animalVisual;
    private int sweepDir;     // +1 last row change went up, −1 down, 0 none yet
    private float? lastRowY;  // row of the previous pass (sets sweepDir)
    private bool openingPass; // first pass of a run starts at the top row (helpers start at the bottom)

    private void OnEnable()
    {
        animalVisual = GetComponent<AnimalVisual>();
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += BeginPlowing;
            RunManager.Instance.OnRunEnded   += EndPlowing;
            if (RunManager.Instance.IsRunActive) BeginPlowing();
        }
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= BeginPlowing;
            RunManager.Instance.OnRunEnded   -= EndPlowing;
        }
        EndPlowing();
    }

    private void BeginPlowing()
    {
        EndPlowing();
        sweepDir = 0;
        lastRowY = null;
        openingPass = true;
        loop = StartCoroutine(PlowLoop());
    }

    private void EndPlowing()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
        if (animalVisual != null) animalVisual.PauseWander = false; // hand wander back
    }

    private IEnumerator PlowLoop()
    {
        var wait = new WaitForSeconds(scanInterval);
        while (true)
        {
            // Wander until there's soil to plow, then give it a beat before reacting.
            while (!HasWork()) yield return wait;
            yield return new WaitForSeconds(noticeDelay);

            while (TryPlanPass(out PlowPass pass))
            {
                if (animalVisual != null) animalVisual.PauseWander = true;

                Vector3 start = new Vector3(pass.startX, pass.rowY, 0f) + hitchOffset;
                yield return WalkTo(start, travelSpeed);
                if (lastRowY.HasValue && Mathf.Abs(pass.rowY - lastRowY.Value) >= PlowPlanner.RowTolerance)
                    sweepDir = pass.rowY > lastRowY.Value ? 1 : -1;
                lastRowY = pass.rowY;

                yield return PlowRow(pass);
            }

            if (animalVisual != null) animalVisual.PauseWander = false; // nothing left → wander
        }
    }

    private IEnumerator WalkTo(Vector3 target, float speed)
    {
        while ((transform.position - target).sqrMagnitude > 0.0025f)
        {
            Vector3 dir = target - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * SpeedMultiplier * Time.deltaTime);
            if (animalVisual != null) animalVisual.DriveMovementAnim(dir, true);
            yield return null;
        }
        transform.position = target;
    }

    /// <summary>Walk the whole row, tilling each untilled tile as the plough passes its centre.</summary>
    private IEnumerator PlowRow(PlowPass pass)
    {
        // The row's tiles in the order the horse will reach them.
        var rowTiles = new List<SoilTile>();
        foreach (SoilTile t in PlowableTiles())
            if (Mathf.Abs(t.transform.position.y - pass.rowY) < PlowPlanner.RowTolerance) rowTiles.Add(t);
        int dir = pass.Direction;
        rowTiles.Sort((a, b) => dir * a.transform.position.x.CompareTo(b.transform.position.x));

        Vector3 end = new Vector3(pass.endX, pass.rowY, 0f) + hitchOffset;
        int next = 0;
        while (true)
        {
            Vector3 heading = end - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, end, plowSpeed * SpeedMultiplier * Time.deltaTime);
            if (animalVisual != null) animalVisual.DriveMovementAnim(heading, true);

            float x = transform.position.x;
            while (next < rowTiles.Count && rowTiles[next] != null
                   && (x - rowTiles[next].transform.position.x) * dir >= 0f)
            {
                Till(rowTiles[next]);
                next++;
            }

            if ((transform.position - end).sqrMagnitude <= 0.0001f) break;
            yield return null;
        }

        // Anything the loop skipped (e.g. a tile destroyed mid-pass is null) — finish the row cleanly.
        for (; next < rowTiles.Count; next++) Till(rowTiles[next]);
    }

    private void Till(SoilTile tile)
    {
        if (tile == null || tile.State != TileState.Untilled) return;
        if (!tile.TillByHelper()) return; // blocked (sprinkler) tiles refuse
        if (RunStats.Instance != null)
            RunStats.Instance.AddAnimalPlow(animalVisual != null && animalVisual.Data != null ? animalVisual.Data.animalID : "horse");
    }

    // ───────── Planning ─────────

    /// <summary>Tiles the horse may plow: unlocked, not under equipment, in a zone that's being farmed.</summary>
    private static IEnumerable<SoilTile> PlowableTiles()
    {
        if (FarmGrid.Instance == null) yield break;
        foreach (SoilTile t in FarmGrid.Instance.GetAllUnlockedTiles())
        {
            if (t == null || t.IsBlocked) continue;
            if (HelperManager.Instance != null && !HelperManager.Instance.ZoneIsWorkable(t.ZoneID)) continue;
            yield return t;
        }
    }

    private static bool HasWork()
    {
        foreach (SoilTile t in PlowableTiles())
            if (t.State == TileState.Untilled) return true;
        return false;
    }

    private bool TryPlanPass(out PlowPass pass)
    {
        var tiles = new List<PlowTile>();
        foreach (SoilTile t in PlowableTiles())
        {
            Vector3 p = t.transform.position;
            tiles.Add(new PlowTile(p.x, p.y, t.State == TileState.Untilled));
        }
        Vector3 hooves = transform.position - hitchOffset;
        bool planned = PlowPlanner.TryPlanPass(PlowPlanner.BuildRows(tiles), hooves.x, hooves.y, sweepDir, out pass,
                                               topFirst: openingPass);
        if (planned && openingPass)
        {
            openingPass = false; // only the very first row; nearest-row snaking from here on
            sweepDir = -1;       // working downward from the top
        }
        return planned;
    }
}
