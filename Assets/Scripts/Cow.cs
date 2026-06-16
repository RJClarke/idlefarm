using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// During-run behavior for the Cow animal. Once a run is active, periodically picks
/// a random mature crop, walks to it, eats it (= removes plant, awards compost lump).
/// The compost lump scales with the Cow Run Yield research bonus.
/// </summary>
public class Cow : MonoBehaviour
{
    [Header("Eating Behavior")]
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float minIntervalSecs = 30f;
    [SerializeField] private float maxIntervalSecs = 60f;
    [Tooltip("Compost lump per eaten crop at L0 Run Yield.")]
    [SerializeField] private int baseLumpPerEat = 15;

    private Coroutine loop;

    private void OnEnable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += BeginEatingLoop;
            RunManager.Instance.OnRunEnded   += EndEatingLoop;
            if (RunManager.Instance.IsRunActive) BeginEatingLoop();
        }
    }

    private void OnDisable()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= BeginEatingLoop;
            RunManager.Instance.OnRunEnded   -= EndEatingLoop;
        }
        EndEatingLoop();
    }

    private void BeginEatingLoop()
    {
        EndEatingLoop();
        loop = StartCoroutine(EatLoop());
    }

    private void EndEatingLoop()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
    }

    private IEnumerator EatLoop()
    {
        while (true)
        {
            float wait = Random.Range(minIntervalSecs, maxIntervalSecs);
            yield return new WaitForSeconds(wait);

            Plant target = PickRandomMaturePlant();
            if (target == null) continue;

            yield return WalkTo(target.transform.position);
            if (target == null) continue; // may have been harvested mid-walk

            EatPlant(target);
        }
    }

    private Plant PickRandomMaturePlant()
    {
        if (FarmGrid.Instance == null) return null;
        var occupied = FarmGrid.Instance.GetOccupiedTiles();
        var matures = new List<Plant>();
        foreach (var tile in occupied)
        {
            if (tile.CurrentPlant == null) continue;
            Plant p = tile.CurrentPlant.GetComponent<Plant>();
            if (p != null && p.IsHarvestable) matures.Add(p);
        }
        if (matures.Count == 0) return null;
        return matures[Random.Range(0, matures.Count)];
    }

    private IEnumerator WalkTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>
    /// Estimate the compost the cow would have produced by eating crops over an offline
    /// stretch of in-game time (real offline × game speed). Fudged: assumes crops were always
    /// available to eat. Returns the lump total; does NOT award it (caller does).
    /// </summary>
    public int EstimateOfflineEatingCompost(double inGameSeconds)
    {
        if (inGameSeconds <= 0) return 0;
        float avgInterval = (minIntervalSecs + maxIntervalSecs) * 0.5f;
        if (avgInterval <= 0f) return 0;

        int eats = Mathf.FloorToInt((float)(inGameSeconds / avgInterval));
        if (eats <= 0) return 0;

        int lump = baseLumpPerEat;
        if (ResearchManager.Instance != null)
            lump = Mathf.RoundToInt(lump * (1f + ResearchManager.Instance.GetBonus(Research.StatKey.CowRunYield)));
        return eats * lump;
    }

    private void EatPlant(Plant plant)
    {
        if (plant == null || CurrencyManager.Instance == null) return;

        int lump = baseLumpPerEat;
        if (ResearchManager.Instance != null)
            lump = Mathf.RoundToInt(lump * (1f + ResearchManager.Instance.GetBonus(Research.StatKey.CowRunYield)));

        CurrencyManager.Instance.AddCompost(lump);
        FloatingTextManager.ShowCompost(lump, plant.transform.position);

        // Remove the plant — no money awarded (cow ate it, not harvested).
        if (plant.ParentTile != null) plant.ParentTile.ClearPlant();
        Destroy(plant.gameObject);
    }
}
