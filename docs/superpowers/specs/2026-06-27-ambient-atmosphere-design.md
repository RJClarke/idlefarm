# Ambient Atmosphere — Cloud Shadows & Wind Debris

**Date:** 2026-06-27
**Branch:** feat/run-ender-economy
**Status:** Design approved, ready for implementation plan

## Goal

Make the existing weather feel alive with an **ambient atmosphere layer**: drifting
cloud shadows sweeping across the farm and visible wind (tumbling leaves/debris). The
layer is **cosmetic-only** and runs always, with hooks so the existing
`ThunderstormManager` can intensify it during storm wind phases.

Fog is explicitly **out of scope** for now (parked as a future add once the base exists).

## Context — existing weather stack

- **`WindController`** (`[ExecuteAlways]`): publishes global shader uniforms
  `_GlobalWindMul` (ambient breeze + Perlin gusts + storm boost) and `_WindTime`
  (unscaled). Read by the **`WindSway`** shader that bends crop/tree sprites.
- **`ThunderstormManager`**: wave-cadenced storms (wind/rain/lightning phases) with
  gameplay damage; exposes `IsWindActive` / `IsStormActive`.
- **`RainOverlayUI`**: screen-space dark overlay (ScreenSpaceOverlay Canvas) + a
  world-space leaf/rain `ParticleSystem` whose emitter tracks `Camera.main`.
- **`WeatherData`** (ScriptableObject): central tuning for all weather values.

The atmosphere layer reuses these patterns and the existing wind globals — it does
**not** introduce a competing wind source.

## Architecture

A single new always-on driver, mirroring `WindController`:

### `AtmosphereController` (`[ExecuteAlways]`)
The ambient brain. Responsibilities:
- Derive a shared **wind direction + strength** from the existing `_GlobalWindMul`
  global, so clouds, shadows, and leaves drift the same way as sprite sway. One
  source of truth.
- Spawn / recycle **cloud-shadow patches** and **wind debris (leaves)**.
- Maintain an **intensity** value (0–1): ambient baseline, ramped up by
  `ThunderstormManager` during wind phases (same easing approach as
  `WindController.stormBoost`). Cosmetic only — never touches damage logic.
- Spawner follows `Camera.main` (same trick as `RainOverlayUI`) so coverage persists
  as the camera pans between Town / Farm / Market.

### `WeatherData` additions (no new asset)
A new "Ambient Atmosphere" header:
- `ShadowStyle` enum: `Soft | Dithered | TintDip`
- Patch tuning: count, size range, opacity, drift speed
- Debris tuning: emission rate, leaf sprites, flutter/rotation
- Storm multipliers: how much intensity ramps shadow count/darkness/speed and debris
  during storm wind

## Sub-systems

### 1. Cloud shadows (3 switchable styles)
Selected via `WeatherData.ShadowStyle` so all three can be evaluated **live in the
Editor** (no browser mockups — the look depends on the real pixel art under the Pixel
Perfect Camera).

- **Soft** — patches use a blurred blob texture, multiply-blended; smooth cozy shadows.
- **Dithered** — patches use a dither-patterned texture (or small dither shader) so
  edges read pixel-native against the 32 PPU art.
- **TintDip** — no patches; a faint full-screen overlay (same pattern as
  `RainOverlayUI`'s canvas) gently darkens/cools the whole scene in slow pulses.

Soft/Dithered share one spawner: patches spawn just off the upwind screen edge, drift
across at `windDir × speed`, recycle when fully off-screen. World-space sprites on a
sorting layer above ground, below entities/HUD.

### 2. Wind debris (leaves)
A lightweight leaf `ParticleSystem` built with the `RainOverlayUI` pattern. Emission
rate + horizontal velocity scale off `_GlobalWindMul`: sparse in a breeze, streaming
during storm wind. A few leaf sprites for variety; gentle rotation + flutter.

### 3. Storm integration
`AtmosphereController` reads `ThunderstormManager.IsWindActive` / `IsStormActive`
(exactly like `WindController`) and ramps `intensity` during wind phases → more/darker/
faster shadows + heavier leaves, easing back after. No damage logic touched.

## Testing & tuning

- `[ContextMenu]` debug hooks (cycle style, force gust) like existing weather components.
- Master switch + `[ExecuteAlways]` preview to tune in-Editor without play mode.
- EditMode tests for pure logic: intensity ramping, patch recycle math, wind-direction
  mapping. Visuals judged live in-engine.

## Out of scope
- Fog (future)
- Any gameplay effect from the ambient layer (damage/moisture stays in
  `ThunderstormManager`)
- Perfectly syncing a visible cloud sprite to each shadow (shadows-only chosen)
