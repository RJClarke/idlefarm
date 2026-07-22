# Graphs — balancing dashboards

Self-contained, interactive HTML pages for tuning/balancing analysis. Each file is a
single `.html` you open directly in a browser (no server, no dependencies).

## Files

- **`animal_balance.html`** — per-animal effectiveness + cross-animal research cost/time.
  - **Chicken card:** coins/day vs research level, with a realistic "amount of use"
    model (check-ins per day + minutes per session) drawn against the theoretical
    perfect-collection ceiling. Separate lines for the cooldown track, the efficiency
    track, and both together. Live cost/time-multiplier sliders + an investment readout.
  - **Compare tab:** total research cost and time to max, one bar per animal
    (Chicken / Rooster / Cow / Dog), so you can eyeball who's the biggest grind.

## Important: these are SNAPSHOTS

The pages bake in a snapshot of the game's ScriptableObject values (animal stats,
research bonus-per-level / tier / base cost & duration, and the global
`ResearchTuning` cost/time multipliers) at generation time. A browser file is
sandboxed and cannot read Unity live.

The in-page sliders let you explore deviations from that baseline without touching
the game. But after you change real tuning in Unity, **regenerate the snapshot** so
the baseline matches the build:

```bash
python Tools/research_extrapolate.py           # sanity-check the raw numbers
# then re-embed the DATA snapshot in animal_balance.html (ask Claude to regenerate)
```

The compute layer (`Tools/research_extrapolate.py`) and these pages apply the exact
in-game formulas:

    cost(L)     = ceil(baseCost * costDifficulty * costMultiplier * L^pCost)
    time(L)     = baseDur * timeDifficulty * timeMultiplier * L^pTime   (seconds)
    egg timer   = baseCooldownMin / (1 + cooldownLevel * bonusPerLevel)
    egg reward  = round(baseReward * (1 + efficiencyLevel * bonusPerLevel))

Horse and Pig are omitted — they currently have no research and placeholder stats.
