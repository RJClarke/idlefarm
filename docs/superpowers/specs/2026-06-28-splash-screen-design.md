# Splash / Title Screen — Design

**Date:** 2026-06-28
**Branch:** feat/run-ender-economy
**Status:** Approved by user ("knock it out"); building inline.

## Goal

A parallaxing title/"login" screen that shows while the game loads in the background, with a jazz
soundtrack and a cute **Start Game** button that boots into the farm once everything is ready.

## Hard constraints
- **No generated/procedural/placeholder assets.** Use only the provided/purchased art + audio.
  See [[feedback_no_generated_assets]].

## Architecture
Separate **`Splash.unity`** scene set as Build Settings index 0 (the app boots here). It async-preloads
`SampleScene` (`allowSceneActivation = false`); the Start button enables once preload ≥ 90%, and clicking
it activates the game scene.

## Scene composition (real art only)
Reuse Background_1's existing layers as-is (Craftpix `Castle Backgrounds Pixel Art/PNG/Background_1`):
- Back→front: **Layer_4 sky+castle** (kept as the real sky; castle stays — not stripped) → **Layer_3 clouds**
  → **Layer_2 hills** → **Layer_1 foreground bushes**, each tiled 3× and auto-scrolling.
- **Hero overlay (real sprites, in front of the sky):** `Silos_1_32x32` (silo) and `Barn_Small_32x32`
  (barn), placed prominently on/near the hills as the focal point. Pixel-art (Point, PPU 21/32).

## Components
- **`SplashParallaxLayer.cs`** — attach per layer; auto-scrolls at its own `speed` in a configurable
  direction and seamlessly wraps using the sprite's tiled width. No Player/camera dependency (the
  Craftpix `ParallaxEffect` requires a "Player" tag and would NRE on a static splash).
- **`SplashController.cs`** — starts the music (AudioSource, looping), runs `LoadSceneAsync("SampleScene")`
  with `allowSceneActivation=false`, enables the Start button at ≥90% load, and on click sets
  `allowSceneActivation=true`. Optional title text.
- **`Splash.unity`** — orthographic splash camera; the 5 sprite layers; `AudioSource` with
  **Jazz Vol2 Thoughtful Sunday Intensity 1** (looping); screen-space Canvas with a LeanTween-pulsing
  **Start** button (+ optional title).

## Boot & testing
- Splash added to Build Settings at index 0.
- **PC testing: open `Splash.unity`, press Play** — no build required. Start → loads `SampleScene`.

## Out of scope (this spec)
- Coach marks / narrative on the splash. Extra branding/logo (modular layers make it easy to add later).

---

## Queued weather follow-ups (same work session, build after splash)
- **Storm wind streaks:** use `Sprites/Environment/Weather/5 Wind/Wind1–4.png` as occasional gust
  swooshes at the START of a storm, flying across in `windDriftDirection` (same way as clouds/leaves).
- **Rain animation:** use `Sprites/Environment/Weather/6 Weather/Rain/1–10.png` frames for rain
  (replace/augment the current procedural particle rain). `Snow1.png` also available for later.
- (Done) Leaves recolored green/yellow-green.
