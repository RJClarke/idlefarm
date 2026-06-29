# Weather System Redesign — Casual Moods + Scaling Storms

**Date:** 2026-06-28
**Branch:** feat/run-ender-economy
**Status:** Design approved by user; ready for implementation plan.

## Goal

Replace today's independent, always-on weather effects with **one shared weather state** that every
effect reads from. Deliver two kinds of weather under one mechanism:
- **Casual weather** (everywhere): occasional, randomized, gentle moods (Clear / Cloudy / Windy).
- **Storm events** (runs only): wave-scheduled, escalating with storm number, where all effects
  intensify *together*.

## Problems with the current system (what we're fixing)
1. **Particles run constantly.** `WindController` publishes an always-on ambient `_GlobalWindMul` (≈1),
   debris emits all the time, gusts/clouds never rest. Should be quiet by default.
2. **Rain looks wrong.** `RainOverlayUI` rain falls on a droopy parabola (gravity + small horizontal
   velocity) and its angle is unrelated to the wind — wind blows horizontal while rain falls vertical.
3. **No cohesion / scaling.** Effects don't share a "how intense is it right now" value, so they can't
   accelerate together or scale with storm number. Cloud shadows also over-dither (too big/pixelated)
   as storms scale.

## Architecture

### `WeatherState` (the single source of truth)
A small struct of live, eased values that every effect consumes:
- `severity` (0–1) — master energy/danger. Drives rain-angle steepness, gust frequency, overlay
  darkness, lightning odds, and serves as the general "accelerate everything" dial.
- `wind` (0–1) — breeze→gale. Drives sprite sway (`_GlobalWindMul`), cloud drift speed, debris speed,
  and the rain's horizontal component.
- `cloudiness` (0–1) — cloud-shadow count / size / opacity.
- `precipitation` (0–1) — rain emission rate + screen-overlay darkness.
- `windDirection` (−1 left / +1 right) — shared by clouds, leaves, gusts, AND rain.

### `WeatherProfile` (data / tuning presets)
A serializable set of *target* channel values (+ direction) with a name. Presets:
- **Clear** — all channels ~0 (default; calm and bright).
- **Cloudy** — `cloudiness` high (~0.7), `wind` low (~0.15), `precipitation` 0, `severity` 0.
- **Windy** — `wind` high (~0.7), `cloudiness` ~0.5 (faster drift), light debris, `precipitation` 0,
  `severity` ~0.1.
- **Storm(N)** — built from storm number: `cloudiness` ~0.9, and `wind`/`precipitation`/`severity` rise
  with N via a curve, e.g. `severity = clamp01(N / 5)` so near-horizontal rain only appears at Storm ~5+.

All profiles + scheduler settings live in **the existing `WeatherData` SO** (`ThunderstormData.asset`),
reorganized to hold the Clear/Cloudy/Windy/Storm profile blocks + scheduler settings, so each mood is
tuned in one place. (Existing storm damage/timing fields on `WeatherData` stay.)

### `WeatherController` (the brain, runs everywhere)
- Holds the live `WeatherState`; each frame eases it toward the active profile's targets at a configurable
  blend speed (this is what makes everything "accelerate together").
- **Casual scheduler:** default Clear; every `rollInterval` (random 2–4 min) rolls a weighted pick
  {Clear/skip, Cloudy, Windy}; on a mood, holds `eventDuration` (random 20–40s) then returns to Clear.
  Runs everywhere (home/town + runs).
- **Storm override:** exposes `BeginStorm(severityForStormN)` / `EndStorm()` called by
  `ThunderstormManager`. A storm sets the Storm(N) profile as active (overriding casual); casual resumes
  after the storm ends.
- Publishes the state for consumers: shader globals (`_GlobalWindMul`, keep `_WindTime`) plus a
  singleton/event (`WeatherController.Instance.State`).

### Pure math: `WeatherMath` (IdleFarm.EconomyCore, unit-tested)
- `EaseChannel(current, target, dt, speed)` — per-channel ease toward target.
- `StormSeverity(stormNumber, stormsToMax)` — the N→severity curve.
- `RainAngleDegrees(wind, severity, maxAngle)` — maps wind+severity to a fall angle (0 = straight down,
  → `maxAngle` near-horizontal at high severity).
- `RollCasualWeather(rngValue, cloudyWeight, windyWeight, clearWeight)` — deterministic weighted pick.

## Effects become pure consumers of `WeatherState`
- **`WindController`** — `_GlobalWindMul = f(state.wind)` (+ small Perlin gust *scaled by* wind). Clear → ~0
  (no constant sway). Keeps publishing unscaled `_WindTime`.
- **`CloudShadowLayer`** — patch count/size/opacity/speed derive from `cloudiness` (+ `wind` for speed).
  Cap max patch size, soften the dither, and tie size to `cloudiness` (not raw storm scale) so it stops
  ballooning/over-dithering.
- **`WindDebrisLayer`** — emission + velocity from `wind`; zero at Clear.
- **`StormGustLayer`** — gust burst frequency/size from `severity`/`wind`; effectively none until windy/stormy.
- **`RainOverlayUI`** — emission rate + overlay darkness from `precipitation`. **Rain is a straight diagonal
  streak** whose angle = `RainAngleDegrees(wind, severity)` in the shared `windDirection`; remove the
  parabola (no gravity-driven curve) so wind and rain share one direction and energy. Steepness scales with
  severity: gentle near-vertical lean early, near-horizontal only at high storm levels.

## Storm integration
`ThunderstormManager` stays the gameplay authority (wave cadence + wind/rain/lightning **damage**, scaling
with storm number as today). It no longer toggles visuals independently — on storm start it calls
`WeatherController.BeginStorm(WeatherMath.StormSeverity(stormNumber, …))`; on end, `EndStorm()`. The eased
blend then ramps wind, rain, clouds, gusts up together. Damage continues to scale with N (and may read
`severity` for consistency).

## The three issues → resolution
1. **Constant particles** → all emitters read `wind`/`precipitation`/`cloudiness`, which are ~0 at Clear;
   particles appear only during a Windy casual window or a storm.
2. **Droopy/mismatched rain** → straight angled streaks driven by the same wind/direction; severity scales
   the angle; horizontal reserved for late storms.
3. **Over-dithered clouds** → size cap + softened dither + count/size tied to `cloudiness`.

## Components / files
- **New:** `Assets/Scripts/EconomyCore/WeatherMath.cs` (+ `WeatherState`, `WeatherProfile` data),
  `Assets/Scripts/WeatherController.cs`, EditMode tests `Assets/Tests/EditMode/WeatherMathTests.cs`.
- **Modified:** `WindController`, `CloudShadowLayer`, `WindDebrisLayer`, `StormGustLayer`, `RainOverlayUI`,
  `ThunderstormManager`, `WeatherData` (becomes/holds the profiles + scheduler config), `AtmosphereController`
  (reads `WeatherState`; may fold into `WeatherController`).
- Scene: a `WeatherController` present everywhere (home/town + run share the one SampleScene, so it lives
  there alongside the existing weather objects).

## Testing
- **EditMode (`WeatherMath`):** channel easing (monotonic, no overshoot), `StormSeverity` curve
  (Storm 1 low → Storm 5 ≈ 1), `RainAngleDegrees` (0 at no wind, rises with wind+severity, capped),
  weighted casual roll (deterministic for given rng + weights).
- **In-engine:** Clear by default = no particles; debug-force Cloudy/Windy and verify the mood; debug-force
  Storm 1 / 3 / 5 and verify escalation and that **rain angle matches the wind** at each level.

## Out of scope (this spec)
- Rain frame-animation art swap (`6 Weather/Rain/1–10`) and snow — separate follow-up.
- New gameplay/danger beyond the existing storm damage (this is visual cohesion + scheduling; damage stays
  in `ThunderstormManager`).
- Weather-type UI/forecast indicators.

## Migration / staging
Implement in reviewable stages: (1) `WeatherMath` + `WeatherState`/`WeatherProfile` + tests; (2)
`WeatherController` + casual scheduler (Clear/Cloudy/Windy), no storm yet; (3) point existing effects at
`WeatherState`; (4) wire `ThunderstormManager` storm override + severity scaling; (5) rain straight-angle
rework + cloud dither/size caps. Each stage keeps the game running.
