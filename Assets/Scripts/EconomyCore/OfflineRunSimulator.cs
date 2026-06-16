using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic, cause-attributed offline run simulator. Pure: no singletons, no scene access,
/// no RNG. Replays the away-window in fixed farm-time ticks and returns an untaxed OfflineRunResult.
/// Tax is applied by the caller via OfflineTax. See spec 2026-06-16.
/// </summary>
public static class OfflineRunSimulator
{
    // ---- deterministic wave/storm context (mirror ThreatWaveManager / WeatherData) ----

    public static int WaveAt(float farmSeconds, OfflineSimTuning t)
        => Mathf.Max(1, Mathf.FloorToInt(farmSeconds / t.waveIntervalSeconds) + 1);

    public static int DeerCount(int wave, OfflineSimTuning t)
    {
        if (wave < t.deerStartWave) return 0;
        int c = Mathf.FloorToInt((float)(wave - t.deerStartWave) / t.deerCountInterval) + 1;
        return Mathf.Min(c, t.maxDeer);
    }

    public static int CrowCount(int wave, OfflineSimTuning t)
    {
        if (wave < t.crowStartWave) return 0;
        int c = Mathf.FloorToInt((float)(wave - t.crowStartWave) / t.crowCountInterval) + 1;
        return Mathf.Min(c, t.maxCrows);
    }

    public static float HungerMult(int wave, OfflineSimTuning t)
        => 1f + (wave - 1) * t.hungerScalePerWave;

    /// <summary>
    /// A storm fires when the run crosses each multiple of stormWaveInterval waves. We model its
    /// lightning phase as a fixed window starting at that wave boundary's farm-time.
    /// </summary>
    public static bool LightningActiveAt(float farmSeconds, OfflineSimTuning t)
    {
        int wave = WaveAt(farmSeconds, t);
        int stormNumber = wave / t.stormWaveInterval;
        if (stormNumber <= 0) return false;
        float stormStart = stormNumber * t.stormWaveInterval * t.waveIntervalSeconds;
        return farmSeconds >= stormStart && farmSeconds < stormStart + t.stormLightningPhaseSeconds;
    }

    // ---- main entry ----
    public static OfflineRunResult Simulate(OfflineSimContext ctx)
    {
        var t = ctx.tuning;
        var r = new OfflineRunResult();

        float farmStart = ctx.startFarmSeconds;
        float farmEnd = farmStart + ctx.awaySeconds * Mathf.Max(1f, ctx.maxGameSpeed);
        float now = farmStart;

        int money = ctx.startMoney;

        // Per-zone occupied tiles: each entry is a tile's growth progress (seconds since planted).
        var occupied = new List<float>[ctx.zones.Count];
        var seeds = new int[ctx.zones.Count];
        for (int z = 0; z < ctx.zones.Count; z++) occupied[z] = new List<float>();

        float plantBudget = 0f; // fractional planting carried across ticks

        // Fractional loss accumulators per zone, per cause. Losses are often < 1 plant per tick
        // (e.g. 1 deer at low waves), so we accumulate the fractional pressure and only remove a
        // whole tile once a unit has built up. Without this, per-tick rounding would discard all
        // sub-1-per-tick losses and deer/crows/drying would never actually remove anything.
        int nz = ctx.zones.Count;
        var accDeer = new float[nz];
        var accCrow = new float[nz];
        var accLight = new float[nz];
        var accDry = new float[nz];
        var accRot = new float[nz];

        while (now < farmEnd && !r.bankrupt)
        {
            float dt = Mathf.Min(t.tickSeconds, farmEnd - now);
            int wave = WaveAt(now, t);
            float hungerMult = HungerMult(wave, t);
            int deer = DeerCount(wave, t);
            int crows = CrowCount(wave, t);
            bool lightning = LightningActiveAt(now, t);

            // 1. advance growth
            for (int z = 0; z < ctx.zones.Count; z++)
                for (int i = 0; i < occupied[z].Count; i++)
                    occupied[z][i] += dt;

            // 2. harvest matured tiles still inside their window
            for (int z = 0; z < ctx.zones.Count; z++)
            {
                var crop = ctx.zones[z].crop;
                for (int i = occupied[z].Count - 1; i >= 0; i--)
                {
                    float p = occupied[z][i];
                    if (p >= crop.growSeconds && p <= crop.growSeconds + crop.harvestWindowSeconds)
                    {
                        money += crop.harvestValue;
                        r.moneyEarned += crop.harvestValue;
                        r.coinsBanked += crop.coinValue;
                        AddHarvest(r, crop.id);
                        occupied[z].RemoveAt(i);
                    }
                }
            }

            // 3. planting (throughput-limited, affordability-gated)
            plantBudget += t.plantsPerSecond * dt;
            for (int z = 0; z < ctx.zones.Count; z++)
            {
                var crop = ctx.zones[z].crop;
                int empty = ctx.zones[z].tileCount - occupied[z].Count;
                while (plantBudget >= 1f && empty > 0)
                {
                    if (seeds[z] <= 0)
                    {
                        int cost = SeedEconomy.BagCost(crop.bagBaseCost, now / 60f, ctx.seedBagDiscount);
                        if (money < cost) break; // can't afford -> stop planting this crop
                        money -= cost;
                        r.moneySpentOnBags += cost;
                        seeds[z] += SeedEconomy.BagSize(crop.bagSize, ctx.seedBagSizeBonus);
                    }
                    seeds[z]--;
                    occupied[z].Add(0f);
                    r.seedsPlanted++;
                    empty--;
                    plantBudget -= 1f;
                }
            }

            // 4. losses by cause — accumulate fractional pressure, remove whole tiles, attribute + compost
            for (int z = 0; z < ctx.zones.Count; z++)
            {
                var crop = ctx.zones[z].crop;
                int growing = occupied[z].Count;

                if (growing == 0)
                {
                    // No plants to lose; drop any fractional backlog so it can't "snap" a future plant.
                    accDeer[z] = accCrow[z] = accLight[z] = accDry[z] = accRot[z] = 0f;
                    continue;
                }

                accDeer[z] += deer * t.baseHunger * hungerMult * t.deerPlantsPerHungerSecond * dt
                              * (1f - ctx.deerLossReduction);
                accCrow[z] += crows * t.crowBaseHunger * hungerMult * t.crowPlantsPerHungerSecond * dt
                              * (1f - ctx.crowLossReduction);
                if (lightning)
                    accLight[z] += (dt / t.lightningStrikeInterval) * t.lightningPlantsPerStrike
                                   * (1f - ctx.lightningLossReduction);
                accDry[z] += growing * t.dryFractionPerSecond * dt * (1f - ctx.dryLossReduction);

                int overWindow = 0;
                for (int i = 0; i < occupied[z].Count; i++)
                    if (occupied[z][i] > crop.growSeconds + crop.harvestWindowSeconds) overWindow++;
                accRot[z] += overWindow * t.rotFractionPerSecond * dt;

                r.eatenByDeer       += TakeWhole(ref accDeer[z],  occupied[z], ref r.compostGained, crop.tier);
                r.eatenByCrows      += TakeWhole(ref accCrow[z],  occupied[z], ref r.compostGained, crop.tier);
                r.struckByLightning += TakeWhole(ref accLight[z], occupied[z], ref r.compostGained, crop.tier);
                r.driedUp           += TakeWhole(ref accDry[z],   occupied[z], ref r.compostGained, crop.tier);
                r.rotted            += TakeWhole(ref accRot[z],   occupied[z], ref r.compostGained, crop.tier);
            }

            // 5. bankruptcy: nothing growing, no seeds, can't afford any bag
            if (IsBankrupt(ctx, occupied, seeds, money, now))
            {
                r.bankrupt = true;
                r.bankruptAtFarmSeconds = now;
                break;
            }

            now += dt;
        }

        r.finalMoney = money;
        r.finalFarmSeconds = r.bankrupt ? r.bankruptAtFarmSeconds : farmEnd;
        return r;
    }

    private static void AddHarvest(OfflineRunResult r, string id)
    {
        r.harvestedByCropId.TryGetValue(id, out int n);
        r.harvestedByCropId[id] = n + 1;
    }

    /// <summary>
    /// Removes floor(acc) tiles (capped by what's available), decrements the accumulator by the number
    /// actually removed (keeping the sub-1 remainder for next tick), adds compost, and returns the count.
    /// </summary>
    private static int TakeWhole(ref float acc, List<float> tiles, ref int compost, int tier)
    {
        int want = Mathf.FloorToInt(acc);
        int removed = 0;
        while (removed < want && tiles.Count > 0)
        {
            tiles.RemoveAt(tiles.Count - 1);
            compost += Mathf.Max(1, tier);
            removed++;
        }
        acc -= removed;
        return removed;
    }

    private static bool IsBankrupt(OfflineSimContext ctx, List<float>[] occupied, int[] seeds, int money, float now)
    {
        for (int z = 0; z < ctx.zones.Count; z++)
            if (occupied[z].Count > 0) return false; // something still growing -> income incoming

        for (int z = 0; z < ctx.zones.Count; z++)
        {
            if (seeds[z] > 0) return false;
            int cost = SeedEconomy.BagCost(ctx.zones[z].crop.bagBaseCost, now / 60f, ctx.seedBagDiscount);
            if (money >= cost) return false; // can still buy and plant
        }
        return true;
    }
}
