# Ambient Atmosphere Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an always-on, cosmetic ambient atmosphere layer — drifting cloud shadows (3 switchable styles) and visible wind (tumbling leaves) — that intensifies during storms.

**Architecture:** One `[ExecuteAlways]` `AtmosphereController` MonoBehaviour reads the existing `_GlobalWindMul` shader global and `ThunderstormManager` storm state, and drives two sub-systems: a `CloudShadowLayer` (world-space drifting patches or a screen-space tint pulse) and a `WindDebrisLayer` (leaf particles). All pure math lives in a testable `AtmosphereMath` static class in `IdleFarm.EconomyCore`; tuning lives on the existing `WeatherData` SO.

**Tech Stack:** Unity 6000.x, URP 2D, C#, NUnit EditMode tests, Pixel Perfect Camera (32 PPU, portrait 1080x1920).

## Global Constraints

- Pure, unit-testable logic goes in `Assets/Scripts/EconomyCore/` (assembly `IdleFarm.EconomyCore`); EditMode tests in `Assets/Tests/EditMode/` reference only that assembly.
- The atmosphere layer is **cosmetic-only** — it must never apply damage, moisture, or any gameplay effect. It only *reads* `_GlobalWindMul` and `ThunderstormManager.IsWindActive`/`IsStormActive`.
- Do not introduce a second wind source. Drift/sway amplitude derives from the existing `_GlobalWindMul` global published by `WindController`.
- Use `Time.unscaledDeltaTime` / unscaled time for visual motion (game-speed agnostic), matching `WindController`.
- MonoBehaviours that preview in-editor use `[ExecuteAlways]`, matching `WindController`.
- Commit after each task. Git commits are pre-approved.

---

### Task 1: ShadowStyle enum + AtmosphereMath (pure logic)

**Files:**
- Create: `Assets/Scripts/EconomyCore/AtmosphereMath.cs`
- Test: `Assets/Tests/EditMode/AtmosphereMathTests.cs`

**Interfaces:**
- Produces:
  - `enum ShadowStyle { Soft, Dithered, TintDip }`
  - `static float AtmosphereMath.EaseIntensity(float current, float target, float dt, float lerpSpeed)`
  - `static float AtmosphereMath.DriftSpeed(float baseSpeed, float windMul, float intensity, float stormSpeedMul)`
  - `static float AtmosphereMath.EmissionRate(float baseRate, float windMul, float intensity, float stormRateMul)`
  - `static float AtmosphereMath.SpawnEdgeX(float camX, float camHalfWidth, float patchHalfWidth, float windDirX)`
  - `static bool AtmosphereMath.IsPatchOffscreen(float patchX, float patchHalfWidth, float camX, float camHalfWidth, float windDirX)`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

public class AtmosphereMathTests
{
    [Test]
    public void EaseIntensity_MovesTowardTargetByLerpSpeedTimesDt()
    {
        // 0 -> 1 target, dt 0.5, speed 1  => +0.5
        Assert.AreEqual(0.5f, AtmosphereMath.EaseIntensity(0f, 1f, 0.5f, 1f), 1e-4f);
    }

    [Test]
    public void EaseIntensity_DoesNotOvershootTarget()
    {
        Assert.AreEqual(1f, AtmosphereMath.EaseIntensity(0.9f, 1f, 1f, 5f), 1e-4f);
    }

    [Test]
    public void DriftSpeed_ScalesWithWindAndStormIntensity()
    {
        // base 2, wind 1, intensity 0 => 2
        Assert.AreEqual(2f, AtmosphereMath.DriftSpeed(2f, 1f, 0f, 1.5f), 1e-4f);
        // intensity 1, stormMul 1.5 => 2 * (1 + 1.5) = 5
        Assert.AreEqual(5f, AtmosphereMath.DriftSpeed(2f, 1f, 1f, 1.5f), 1e-4f);
    }

    [Test]
    public void EmissionRate_ScalesWithWindAndStorm()
    {
        Assert.AreEqual(10f, AtmosphereMath.EmissionRate(10f, 1f, 0f, 2f), 1e-4f);
        Assert.AreEqual(30f, AtmosphereMath.EmissionRate(10f, 1f, 1f, 2f), 1e-4f); // 10*(1+2)
    }

    [Test]
    public void SpawnEdgeX_PlacesPatchJustOffUpwindEdge()
    {
        // wind blows left (-1): patches enter from the RIGHT edge.
        // cam 0, halfWidth 10, patchHalf 2 => right edge 10, plus patchHalf => 12
        Assert.AreEqual(12f, AtmosphereMath.SpawnEdgeX(0f, 10f, 2f, -1f), 1e-4f);
        // wind blows right (+1): enter from LEFT => -12
        Assert.AreEqual(-12f, AtmosphereMath.SpawnEdgeX(0f, 10f, 2f, 1f), 1e-4f);
    }

    [Test]
    public void IsPatchOffscreen_TrueOnlyAfterFullyPastDownwindEdge()
    {
        // wind left (-1): downwind edge is the LEFT (-10). Patch fully off when right side < -10,
        // i.e. patchX + patchHalf < -10  => patchX < -12.
        Assert.IsFalse(AtmosphereMath.IsPatchOffscreen(-11f, 2f, 0f, 10f, -1f)); // right side -9, still visible
        Assert.IsTrue (AtmosphereMath.IsPatchOffscreen(-13f, 2f, 0f, 10f, -1f)); // right side -11, gone
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode tests (Unity Test Runner, or `run_tests` MCP tool, filter `AtmosphereMathTests`).
Expected: FAIL — `AtmosphereMath` / `ShadowStyle` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using UnityEngine;

/// <summary>Visual style for the ambient cloud-shadow layer.</summary>
public enum ShadowStyle { Soft, Dithered, TintDip }

/// <summary>
/// Pure, stateless math for the ambient atmosphere layer (cloud shadows + wind debris).
/// No singletons, no Unity objects — unit-testable. Lives in IdleFarm.EconomyCore.
///
/// Drift/emission scale off the global wind multiplier (WindController's _GlobalWindMul)
/// and a 0..1 storm "intensity" that ThunderstormManager ramps up during wind phases.
/// windDirX is a sign (-1 = blows left, +1 = blows right) configured on WeatherData.
/// </summary>
public static class AtmosphereMath
{
    /// <summary>MoveTowards-style ease of an intensity value, clamped so it never overshoots.</summary>
    public static float EaseIntensity(float current, float target, float dt, float lerpSpeed)
        => Mathf.MoveTowards(current, target, Mathf.Max(0f, dt) * Mathf.Max(0f, lerpSpeed));

    /// <summary>Patch/cloud drift speed: base * wind, boosted by storm intensity.</summary>
    public static float DriftSpeed(float baseSpeed, float windMul, float intensity, float stormSpeedMul)
        => baseSpeed * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity) * Mathf.Max(0f, stormSpeedMul));

    /// <summary>Debris particles/second: base, boosted by storm intensity (wind already in particle vel).</summary>
    public static float EmissionRate(float baseRate, float windMul, float intensity, float stormRateMul)
        => baseRate * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity) * Mathf.Max(0f, stormRateMul));

    /// <summary>World X just off the UPWIND screen edge where a new patch should spawn.</summary>
    public static float SpawnEdgeX(float camX, float camHalfWidth, float patchHalfWidth, float windDirX)
    {
        // Wind blows toward sign(windDirX); patches enter from the opposite edge.
        float upwindSign = windDirX < 0f ? 1f : -1f; // blows left => enter from right (+)
        return camX + upwindSign * (camHalfWidth + patchHalfWidth);
    }

    /// <summary>True once a patch has drifted fully past the DOWNWIND screen edge.</summary>
    public static bool IsPatchOffscreen(float patchX, float patchHalfWidth, float camX, float camHalfWidth, float windDirX)
    {
        if (windDirX < 0f) // blows left: gone when its right side is past the left edge
            return patchX + patchHalfWidth < camX - camHalfWidth;
        return patchX - patchHalfWidth > camX + camHalfWidth; // blows right
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run EditMode tests filtered to `AtmosphereMathTests`.
Expected: all 6 PASS.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/EconomyCore/AtmosphereMath.cs" "Assets/Tests/EditMode/AtmosphereMathTests.cs"
git commit -m "feat(weather): AtmosphereMath pure logic + ShadowStyle enum"
```

---

### Task 2: WeatherData — Ambient Atmosphere tuning fields

**Files:**
- Modify: `Assets/Scripts/WeatherData.cs` (append a new region before `OnValidate`)

**Interfaces:**
- Consumes: `ShadowStyle` (Task 1)
- Produces (public serialized fields on `WeatherData`):
  - `bool atmosphereEnabled`
  - `ShadowStyle shadowStyle`
  - `int shadowMaxPatches`, `Vector2 shadowSizeRange`, `float shadowOpacity`, `float shadowBaseDriftSpeed`, `float windDriftDirection`
  - `float shadowStormSpeedMul`, `float shadowStormOpacityMul`
  - `Color tintDipColor`, `float tintDipMaxAlpha`, `float tintDipPeriod`
  - `float debrisBaseRate`, `Sprite[] debrisSprites`, `float debrisStormRateMul`
  - `float atmosphereStormLerpSpeed`

- [ ] **Step 1: Add the fields**

Insert before the `// Runtime Helpers` region in `WeatherData.cs`:

```csharp
    // ─────────────────────────────────────────────────────────────────────
    // Ambient Atmosphere (cosmetic — cloud shadows + wind debris)
    // ─────────────────────────────────────────────────────────────────────

    [Header("Ambient Atmosphere — Master")]
    [Tooltip("Master switch for the always-on ambient cloud-shadow + wind-debris layer.")]
    public bool atmosphereEnabled = true;

    [Tooltip("How fast the storm intensity (0..1) eases in/out for the atmosphere layer.")]
    [Range(0.1f, 5f)]
    public float atmosphereStormLerpSpeed = 0.8f;

    [Header("Ambient — Cloud Shadows")]
    [Tooltip("Soft = blurred multiply patches; Dithered = pixel-native patches; TintDip = whole-scene pulse.")]
    public ShadowStyle shadowStyle = ShadowStyle.Dithered;

    [Tooltip("Max simultaneous drifting shadow patches (Soft/Dithered modes).")]
    [Range(0, 12)]
    public int shadowMaxPatches = 3;

    [Tooltip("Min/Max world-unit width of a shadow patch.")]
    public Vector2 shadowSizeRange = new Vector2(8f, 16f);

    [Tooltip("Peak opacity of a shadow patch at ambient intensity.")]
    [Range(0f, 1f)]
    public float shadowOpacity = 0.25f;

    [Tooltip("Base drift speed (world units/sec) at wind multiplier 1.")]
    [Range(0f, 10f)]
    public float shadowBaseDriftSpeed = 1.2f;

    [Tooltip("Drift direction sign: -1 = clouds blow left, +1 = blow right.")]
    public float windDriftDirection = -1f;

    [Tooltip("Extra drift-speed multiplier at full storm intensity.")]
    [Range(0f, 4f)]
    public float shadowStormSpeedMul = 1.5f;

    [Tooltip("Extra opacity multiplier at full storm intensity.")]
    [Range(0f, 3f)]
    public float shadowStormOpacityMul = 1.4f;

    [Header("Ambient — Cloud Shadows (TintDip style)")]
    [Tooltip("Color the whole scene dips toward when a cloud 'passes over the sun'.")]
    public Color tintDipColor = new Color(0.15f, 0.18f, 0.28f, 1f);

    [Tooltip("Peak alpha of the tint-dip overlay at ambient intensity.")]
    [Range(0f, 0.6f)]
    public float tintDipMaxAlpha = 0.18f;

    [Tooltip("Seconds for one full darken→brighten tint-dip cycle.")]
    [Range(4f, 60f)]
    public float tintDipPeriod = 14f;

    [Header("Ambient — Wind Debris (leaves)")]
    [Tooltip("Base leaf particles/second at wind multiplier 1.")]
    [Range(0f, 30f)]
    public float debrisBaseRate = 3f;

    [Tooltip("Leaf sprites to scatter (random per particle). Leave empty for a tiny generated speck.")]
    public Sprite[] debrisSprites;

    [Tooltip("Extra emission multiplier at full storm intensity.")]
    [Range(0f, 6f)]
    public float debrisStormRateMul = 3f;
```

- [ ] **Step 2: Verify compile**

Refresh Unity, check console for compile errors. Expected: clean compile (the `ThunderstormData.asset` keeps its existing values; new fields take defaults).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/WeatherData.cs"
git commit -m "feat(weather): WeatherData ambient atmosphere tuning fields"
```

---

### Task 3: CloudShadowLayer — drifting patches (Soft/Dithered)

**Files:**
- Create: `Assets/Scripts/CloudShadowLayer.cs`

**Interfaces:**
- Consumes: `AtmosphereMath` (Task 1), `WeatherData` fields (Task 2)
- Produces: `class CloudShadowLayer` with
  - `void Configure(WeatherData data)`
  - `void Tick(float dt, float windMul, float intensity, Camera cam)` — spawns/drifts/recycles patches
  - `void SetActiveStyle(bool patchesVisible)` — hides all patches when style != Soft/Dithered
  - `void Clear()`

This is a plain class (not a MonoBehaviour) owned by `AtmosphereController`; it builds child GameObjects under a provided parent transform.

- [ ] **Step 1: Implement**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a pool of world-space drifting cloud-shadow patches (Soft / Dithered styles).
/// Owned by AtmosphereController. Patches spawn off the upwind edge, drift across at a
/// wind-scaled speed, and recycle when fully off the downwind edge. Multiply-blended so
/// they darken whatever they pass over. Sorting sits above ground, below entities.
/// </summary>
public class CloudShadowLayer
{
    private const int ShadowSortingOrder = 200; // above ground tiles, below the ~1000+ entity band

    private class Patch
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float halfWidth;
        public float baseAlpha;
    }

    private Transform parent;
    private WeatherData data;
    private readonly List<Patch> patches = new List<Patch>();
    private Sprite softSprite;
    private Sprite ditherSprite;
    private Material multiplyMat;
    private bool patchesVisible = true;

    public CloudShadowLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        if (multiplyMat == null) multiplyMat = BuildMultiplyMaterial();
        if (softSprite == null)   softSprite   = BuildBlobSprite(false);
        if (ditherSprite == null) ditherSprite = BuildBlobSprite(true);
    }

    public void SetActiveStyle(bool visible)
    {
        patchesVisible = visible;
        if (!visible) Clear();
    }

    public void Tick(float dt, float windMul, float intensity, Camera cam)
    {
        if (data == null || cam == null || !patchesVisible) return;

        float camX = cam.transform.position.x;
        float camHalf = cam.orthographicSize * cam.aspect;
        float dir = Mathf.Sign(data.windDriftDirection == 0f ? -1f : data.windDriftDirection);
        float speed = AtmosphereMath.DriftSpeed(data.shadowBaseDriftSpeed, windMul, intensity, data.shadowStormSpeedMul);
        float opacityMul = 1f + Mathf.Clamp01(intensity) * data.shadowStormOpacityMul;

        // Drift + recycle existing.
        for (int i = patches.Count - 1; i >= 0; i--)
        {
            Patch p = patches[i];
            Vector3 pos = p.go.transform.position;
            pos.x += -dir * speed * dt; // dir is the upwind sign convention; move downwind
            p.go.transform.position = pos;

            Color c = p.sr.color;
            c.a = Mathf.Clamp01(p.baseAlpha * opacityMul);
            p.sr.color = c;

            if (AtmosphereMath.IsPatchOffscreen(pos.x, p.halfWidth, camX, camHalf, data.windDriftDirection))
            {
                Object.Destroy(p.go);
                patches.RemoveAt(i);
            }
        }

        // Spawn up to the cap.
        while (patches.Count < data.shadowMaxPatches)
            patches.Add(SpawnPatch(cam, camX, camHalf));
    }

    private Patch SpawnPatch(Camera cam, float camX, float camHalf)
    {
        float width = Random.Range(data.shadowSizeRange.x, data.shadowSizeRange.y);
        float half = width * 0.5f;
        float camY = cam.transform.position.y;
        float camHalfH = cam.orthographicSize;

        var go = new GameObject("CloudShadowPatch");
        go.transform.SetParent(parent, false);
        float x = AtmosphereMath.SpawnEdgeX(camX, camHalf, half, data.windDriftDirection);
        float y = camY + Random.Range(-camHalfH, camHalfH);
        go.transform.position = new Vector3(x, y, 0f);
        float aspect = Random.Range(0.5f, 0.8f); // wider than tall
        go.transform.localScale = new Vector3(width, width * aspect, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = data.shadowStyle == ShadowStyle.Dithered ? ditherSprite : softSprite;
        sr.material = multiplyMat;
        sr.sortingOrder = ShadowSortingOrder;
        sr.color = new Color(1f, 1f, 1f, data.shadowOpacity);

        return new Patch { go = go, sr = sr, halfWidth = half, baseAlpha = data.shadowOpacity };
    }

    public void Clear()
    {
        foreach (var p in patches) if (p.go != null) Object.Destroy(p.go);
        patches.Clear();
    }

    // ── Procedural assets (no art dependency) ──────────────────────────────

    /// <summary>Multiply blend so white=no change, black=darken. Uses a built-in particle-multiply path.</summary>
    private static Material BuildMultiplyMaterial()
    {
        // Sprites/Default with Multiply blend via custom blend isn't available; use a tiny shader-free
        // approach: a sprite whose RGB is the shadow color and alpha is the mask, drawn with normal
        // alpha blending. This darkens by overlaying a translucent dark sprite (good enough at low alpha).
        var mat = new Material(Shader.Find("Sprites/Default"));
        return mat;
    }

    /// <summary>
    /// A radial blob: opaque-ish dark center fading to transparent edges. Dithered version
    /// quantizes the falloff into a few hard bands + a checker pattern so it reads pixel-native.
    /// </summary>
    private static Sprite BuildBlobSprite(bool dithered)
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = dithered ? FilterMode.Point : FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color32[N * N];
        Vector2 c = new Vector2(N * 0.5f, N * 0.5f);
        float maxR = N * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxR; // 0 center .. 1 edge
            float a = Mathf.Clamp01(1f - d);
            a = a * a; // softer edge
            if (dithered)
            {
                a = Mathf.Round(a * 4f) / 4f;              // quantize to 4 bands
                if (((x + y) & 1) == 0) a *= 0.6f;          // checker dither on alternating pixels
            }
            // Dark sprite (RGB near black), alpha is the mask. Drawn translucent → darkens scene.
            px[y * N + x] = new Color(0.05f, 0.06f, 0.10f, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N); // 1 world unit = N px → scaled by transform
    }
}
```

> Implementer note: true GPU multiply blend needs a custom shader; per YAGNI we approximate with a low-alpha dark sprite over the scene, which reads as a shadow at the opacities here. If the look needs true multiply later, add a `Custom/ShadowMultiply` shader (`Blend DstColor Zero`) and assign it in `BuildMultiplyMaterial`.

- [ ] **Step 2: Verify compile** — refresh Unity, console clean.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/CloudShadowLayer.cs"
git commit -m "feat(weather): CloudShadowLayer drifting patch pool (soft/dithered)"
```

---

### Task 4: TintDipLayer — whole-scene pulse style

**Files:**
- Create: `Assets/Scripts/TintDipLayer.cs`

**Interfaces:**
- Consumes: `WeatherData` (Task 2), `AtmosphereMath` not required.
- Produces: `class TintDipLayer` with `void Configure(WeatherData)`, `void Tick(float unscaledTime, float intensity)`, `void SetActive(bool)`, `void Clear()`.

Builds a ScreenSpaceOverlay Canvas + full-screen `Image` (mirrors `RainOverlayUI.BuildOverlayCanvas`) and pulses its alpha with a sine over `tintDipPeriod`, scaled up by storm intensity.

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Cloud passes over the sun" style: a faint full-screen overlay that slowly pulses
/// darker/lighter. No moving patches. Mirrors RainOverlayUI's screen-space canvas pattern.
/// </summary>
public class TintDipLayer
{
    private Transform parent;
    private WeatherData data;
    private Canvas canvas;
    private Image image;

    public TintDipLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        if (canvas == null) Build();
    }

    public void SetActive(bool active)
    {
        if (canvas != null) canvas.gameObject.SetActive(active);
        if (!active && image != null) SetAlpha(0f);
    }

    public void Tick(float unscaledTime, float intensity)
    {
        if (data == null || image == null) return;
        // Slow sine 0..1 over the period; biased so the scene spends more time bright than dark.
        float phase = Mathf.Sin(unscaledTime * (2f * Mathf.PI / Mathf.Max(1f, data.tintDipPeriod)));
        float pulse = Mathf.Clamp01(phase) ;                 // only the darkening half
        float peak  = data.tintDipMaxAlpha * (1f + Mathf.Clamp01(intensity) * data.shadowStormOpacityMul);
        SetAlpha(pulse * peak);
    }

    public void Clear() { if (image != null) SetAlpha(0f); }

    private void SetAlpha(float a)
    {
        Color c = data != null ? data.tintDipColor : Color.black;
        c.a = a;
        image.color = c;
    }

    private void Build()
    {
        var canvasGO = new GameObject("TintDipCanvas");
        canvasGO.transform.SetParent(parent, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 48; // just below RainOverlayUI's 50
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imageGO = new GameObject("TintDip");
        imageGO.transform.SetParent(canvasGO.transform, false);
        image = imageGO.AddComponent<Image>();
        image.raycastTarget = false;
        var rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        SetAlpha(0f);
    }
}
```

- [ ] **Step 2: Verify compile** — refresh Unity, console clean.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/TintDipLayer.cs"
git commit -m "feat(weather): TintDipLayer whole-scene cloud-pass pulse"
```

---

### Task 5: WindDebrisLayer — leaf particles

**Files:**
- Create: `Assets/Scripts/WindDebrisLayer.cs`

**Interfaces:**
- Consumes: `AtmosphereMath` (Task 1), `WeatherData` (Task 2)
- Produces: `class WindDebrisLayer` with `void Configure(WeatherData)`, `void Tick(float windMul, float intensity, Camera cam)`, `void SetActive(bool)`, `void Clear()`.

Builds one `ParticleSystem` (mirrors `RainOverlayUI.BuildRainParticles`) emitting leaf sprites. Emitter tracks the camera; emission rate + horizontal velocity scale off wind & intensity.

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;

/// <summary>
/// Visible wind: a sparse stream of tumbling leaves that thickens as the global wind
/// multiplier and storm intensity rise. Built like RainOverlayUI's particle system;
/// emitter tracks Camera.main. Cosmetic only.
/// </summary>
public class WindDebrisLayer
{
    private Transform parent;
    private WeatherData data;
    private ParticleSystem ps;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.VelocityOverLifetimeModule vel;

    public WindDebrisLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        if (ps == null) Build();
    }

    public void SetActive(bool active)
    {
        if (ps == null) return;
        if (active) { if (!ps.isPlaying) ps.Play(); }
        else { ps.Stop(); ps.Clear(); }
    }

    public void Tick(float windMul, float intensity, Camera cam)
    {
        if (ps == null || data == null) return;

        float rate = AtmosphereMath.EmissionRate(data.debrisBaseRate, windMul, intensity, data.debrisStormRateMul);
        emission.rateOverTime = rate;

        float dir = Mathf.Sign(data.windDriftDirection == 0f ? -1f : data.windDriftDirection);
        float hSpeed = -dir * (4f + 10f * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity)));
        vel.x = new ParticleSystem.MinMaxCurve(hSpeed);

        if (cam != null)
        {
            float camHalfH = cam.orthographicSize;
            float camHalfW = cam.orthographicSize * cam.aspect;
            // Emit from the upwind edge, full screen height.
            float x = cam.transform.position.x + dir * (camHalfW + 1f);
            ps.transform.position = new Vector3(x, cam.transform.position.y, -1f);
            var shape = ps.shape;
            shape.scale = new Vector3(0.1f, camHalfH * 2f, 1f);
        }
    }

    public void Clear() { if (ps != null) { ps.Stop(); ps.Clear(); } }

    private void Build()
    {
        var go = new GameObject("WindDebris");
        go.transform.SetParent(parent, false);
        ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.55f, 0.45f, 0.2f, 0.9f);

        emission = ps.emission;
        emission.rateOverTime = data.debrisBaseRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(0.1f, 20f, 1f);

        vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-8f);
        vel.y = new ParticleSystem.MinMaxCurve(-1f);

        // Flutter: rotate over lifetime so leaves tumble.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 250; // above shadow patches, below HUD
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        if (data.debrisSprites != null && data.debrisSprites.Length > 0)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            for (int i = 0; i < data.debrisSprites.Length; i++) tsa.AddSprite(data.debrisSprites[i]);
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f); // pick one sprite per particle
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, data.debrisSprites.Length);
        }
        ps.Stop();
    }
}
```

- [ ] **Step 2: Verify compile** — refresh Unity, console clean.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/WindDebrisLayer.cs"
git commit -m "feat(weather): WindDebrisLayer leaf particles scaled by wind"
```

---

### Task 6: AtmosphereController — the always-on driver

**Files:**
- Create: `Assets/Scripts/AtmosphereController.cs`

**Interfaces:**
- Consumes: `AtmosphereMath` (1), `WeatherData` (2), `CloudShadowLayer` (3), `TintDipLayer` (4), `WindDebrisLayer` (5).
- Reads global `_GlobalWindMul` / `_WindTime` (published by `WindController`) and `ThunderstormManager.Instance.IsWindActive`/`IsStormActive`.

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;

/// <summary>
/// Always-on cosmetic atmosphere brain. Reads the global wind multiplier (WindController's
/// _GlobalWindMul) and storm state, eases a 0..1 storm "intensity", and drives the cloud-shadow
/// and wind-debris layers. [ExecuteAlways] so it previews in the editor like WindController.
/// Cosmetic only — never applies gameplay effects.
/// </summary>
[ExecuteAlways]
public class AtmosphereController : MonoBehaviour
{
    [SerializeField] private WeatherData weatherData;

    private static readonly int WindID = Shader.PropertyToID("_GlobalWindMul");
    private static readonly int TimeID = Shader.PropertyToID("_WindTime");

    private CloudShadowLayer shadows;
    private TintDipLayer tintDip;
    private WindDebrisLayer debris;
    private float intensity;
    private ShadowStyle lastStyle;

    private void OnEnable()
    {
        shadows ??= new CloudShadowLayer(transform);
        tintDip ??= new TintDipLayer(transform);
        debris  ??= new WindDebrisLayer(transform);
        Reconfigure();
    }

    private void OnDisable()
    {
        shadows?.Clear();
        tintDip?.Clear();
        debris?.Clear();
    }

    private void Reconfigure()
    {
        if (weatherData == null) return;
        shadows.Configure(weatherData);
        tintDip.Configure(weatherData);
        debris.Configure(weatherData);
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        bool on = weatherData != null && weatherData.atmosphereEnabled;
        bool patches = on && weatherData.shadowStyle != ShadowStyle.TintDip;
        bool tint    = on && weatherData.shadowStyle == ShadowStyle.TintDip;
        shadows.SetActiveStyle(patches);
        tintDip.SetActive(tint);
        debris.SetActive(on);
        lastStyle = weatherData != null ? weatherData.shadowStyle : ShadowStyle.Soft;
    }

    private void Update()
    {
        if (weatherData == null) return;
        if (!weatherData.atmosphereEnabled) { OnDisable(); return; }

        // React to a style switch made live in the inspector.
        if (weatherData.shadowStyle != lastStyle) ApplyStyle();

        float windMul = Mathf.Max(0f, Shader.GetGlobalFloat(WindID));
        float wTime   = Shader.GetGlobalFloat(TimeID);
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // Ease storm intensity (cosmetic) from ThunderstormManager state.
        float target = 0f;
        if (Application.isPlaying && ThunderstormManager.Instance != null &&
            (ThunderstormManager.Instance.IsWindActive || ThunderstormManager.Instance.IsStormActive))
            target = 1f;
        intensity = AtmosphereMath.EaseIntensity(intensity, target, dt, weatherData.atmosphereStormLerpSpeed);

        Camera cam = Camera.main;
        if (weatherData.shadowStyle == ShadowStyle.TintDip)
            tintDip.Tick(wTime, intensity);
        else
            shadows.Tick(dt, windMul, intensity, cam);

        debris.Tick(windMul, intensity, cam);
    }

#if UNITY_EDITOR
    [ContextMenu("Atmosphere: Cycle Shadow Style")]
    private void CycleStyle()
    {
        if (weatherData == null) return;
        weatherData.shadowStyle = (ShadowStyle)(((int)weatherData.shadowStyle + 1) % 3);
        ApplyStyle();
        Debug.Log($"[Atmosphere] Style → {weatherData.shadowStyle}");
    }

    [ContextMenu("Atmosphere: Force Storm Intensity")]
    private void ForceStormIntensity() => intensity = 1f;
#endif
}
```

> Implementer note: `??=` on Unity objects is fine here because these are plain C# classes, not `UnityEngine.Object`.

- [ ] **Step 2: Verify compile** — refresh Unity, console clean.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/AtmosphereController.cs"
git commit -m "feat(weather): AtmosphereController always-on cosmetic driver"
```

---

### Task 7: Scene wiring + live verification

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (add the `AtmosphereController` GameObject, assign `ThunderstormData.asset`)

- [ ] **Step 1: Add the GameObject**

In `SampleScene`, create an empty GameObject `AtmosphereController` (sibling of the existing weather/wind objects). Add the `AtmosphereController` component. Assign `Assets/Data/Threats/ThunderstormData.asset` to its `Weather Data` field.

- [ ] **Step 2: Verify ambient look (Dithered default)**

Enter play mode (or rely on `[ExecuteAlways]` in the Game view). Confirm: shadow patches drift across the farm in the wind direction; leaves stream sparsely. No console errors.

- [ ] **Step 3: Cycle styles**

Right-click the component → "Atmosphere: Cycle Shadow Style"; confirm Soft (blurry patches), Dithered (pixel patches), TintDip (whole-scene pulse, no patches) each render and only one is active at a time.

- [ ] **Step 4: Verify storm ramp**

Right-click `ThunderstormManager` → "Debug: Force Thunderstorm (Storm 1)" (or "Atmosphere: Force Storm Intensity"). Confirm shadows darken/speed up and leaves thicken, then ease back after the storm.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scenes/SampleScene.unity"
git commit -m "feat(weather): wire AtmosphereController into SampleScene"
```

---

## Self-Review

**Spec coverage:**
- Ambient-first always-on driver → Task 6 ✓
- Cloud shadows, 3 switchable styles (Soft/Dithered/TintDip) → Tasks 3, 4, 6 ✓
- Wind debris (leaves) scaling off `_GlobalWindMul` → Task 5 ✓
- Storm intensity hook (reads `IsWindActive`/`IsStormActive`, eases) → Tasks 1, 6 ✓
- Tuning on `WeatherData`, no new asset → Task 2 ✓
- Camera-following coverage across Town/Farm/Market → Tasks 3, 5 ✓
- Cosmetic-only (no gameplay) → enforced throughout; no damage/moisture calls ✓
- `[ExecuteAlways]` editor preview + `[ContextMenu]` debug → Task 6 ✓
- EditMode tests for pure logic → Task 1 ✓
- Fog out of scope → not implemented ✓

**Placeholder scan:** none — all steps carry full code/commands.

**Type consistency:** `ShadowStyle` (Soft/Dithered/TintDip), `AtmosphereMath` signatures, and the three layer classes' `Configure/Tick/SetActive(Style)/Clear` methods are used consistently across Tasks 1–7. Note the two distinct method names by design: `CloudShadowLayer.SetActiveStyle(bool)` vs `TintDipLayer.SetActive(bool)` / `WindDebrisLayer.SetActive(bool)` — Task 6 calls each by its correct name.
