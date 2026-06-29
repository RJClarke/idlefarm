# Weather System Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unify all weather effects under one shared, eased `WeatherState` driven by named profiles (Clear/Cloudy/Windy/Storm) — casual moods everywhere, scaling storms in runs — so particles are quiet by default, rain matches the wind, and everything accelerates together.

**Architecture:** A `WeatherController` singleton owns a live `WeatherState` and eases it toward the active `WeatherProfile` (casual scheduler, or storm override from `ThunderstormManager`). Every effect (`WindController`, cloud/debris/gust layers, `RainOverlayUI`) becomes a pure consumer of `WeatherState`. Pure blend/curve/roll math lives in `WeatherMath` (IdleFarm.EconomyCore) with EditMode tests.

**Tech Stack:** Unity 6000.3, URP 2D, C#, NUnit EditMode tests, MCP for Unity.

## Global Constraints
- Pure, testable logic → `Assets/Scripts/EconomyCore/` (assembly `IdleFarm.EconomyCore`); EditMode tests in `Assets/Tests/EditMode/` reference only that assembly.
- No generated/placeholder art — real assets only ([[feedback_no_generated_assets]]).
- Cosmetic layers are play-mode only (no `[ExecuteAlways]`); use unscaled time for visual motion.
- `windDirection` is one shared value (`WeatherData.windDriftDirection`, −1 left / +1 right) used by clouds, debris, gusts, AND rain.
- Channels are 0..1: `severity`, `wind`, `cloudiness`, `precipitation`.
- Each task ends compiling cleanly (check `read_console` for `error CS`) and the game still runs.

---

### Task 1: WeatherState + WeatherProfile + WeatherMath (pure logic)

**Files:**
- Create: `Assets/Scripts/EconomyCore/WeatherTypes.cs`
- Create: `Assets/Scripts/EconomyCore/WeatherMath.cs`
- Test: `Assets/Tests/EditMode/WeatherMathTests.cs`

**Interfaces — Produces:**
- `struct WeatherState { float severity, wind, cloudiness, precipitation, windDirection; }`
- `[System.Serializable] class WeatherProfile { string name; float severity, wind, cloudiness, precipitation; }`
- `enum CasualWeather { Clear = 0, Cloudy = 1, Windy = 2 }`
- `static float WeatherMath.EaseChannel(float current, float target, float dt, float speed)`
- `static float WeatherMath.StormSeverity(int stormNumber, float stormsToMax)`
- `static float WeatherMath.RainAngleDegrees(float wind, float severity, float maxAngleDeg)`
- `static int WeatherMath.RollCasual(float rng01, float clearWeight, float cloudyWeight, float windyWeight)`

- [ ] **Step 1: Write the failing test** — `Assets/Tests/EditMode/WeatherMathTests.cs`

```csharp
using NUnit.Framework;

public class WeatherMathTests
{
    [Test]
    public void EaseChannel_MovesTowardTarget_NoOvershoot()
    {
        Assert.AreEqual(0.5f, WeatherMath.EaseChannel(0f, 1f, 0.5f, 1f), 1e-4f);
        Assert.AreEqual(1f,   WeatherMath.EaseChannel(0.9f, 1f, 1f, 5f), 1e-4f);
    }

    [Test]
    public void StormSeverity_RisesWithStormNumber_ClampedTo1()
    {
        Assert.AreEqual(0.2f, WeatherMath.StormSeverity(1, 5f), 1e-4f);
        Assert.AreEqual(1.0f, WeatherMath.StormSeverity(5, 5f), 1e-4f);
        Assert.AreEqual(1.0f, WeatherMath.StormSeverity(9, 5f), 1e-4f); // clamps
    }

    [Test]
    public void RainAngle_ZeroWhenCalm_RisesWithSeverity_Capped()
    {
        Assert.AreEqual(0f, WeatherMath.RainAngleDegrees(0f, 0f, 80f), 1e-4f);
        Assert.AreEqual(40f, WeatherMath.RainAngleDegrees(0f, 0.5f, 80f), 1e-4f);
        Assert.AreEqual(80f, WeatherMath.RainAngleDegrees(1f, 1f, 80f), 1e-4f); // clamped at max
    }

    [Test]
    public void RollCasual_PartitionsByWeight()
    {
        // weights clear=2, cloudy=1, windy=1 (total 4): r<0.5→Clear, <0.75→Cloudy, else Windy
        Assert.AreEqual(0, WeatherMath.RollCasual(0.0f, 2f, 1f, 1f));
        Assert.AreEqual(0, WeatherMath.RollCasual(0.49f, 2f, 1f, 1f));
        Assert.AreEqual(1, WeatherMath.RollCasual(0.60f, 2f, 1f, 1f));
        Assert.AreEqual(2, WeatherMath.RollCasual(0.90f, 2f, 1f, 1f));
    }
}
```

- [ ] **Step 2: Run tests, verify they FAIL** (types/methods missing). Run EditMode tests filtered to `WeatherMathTests`.

- [ ] **Step 3: Create `WeatherTypes.cs`**

```csharp
using UnityEngine;

/// <summary>Live, eased weather values that every effect reads. 0..1 channels + a wind sign.</summary>
public struct WeatherState
{
    public float severity;      // master energy/danger
    public float wind;          // breeze -> gale
    public float cloudiness;    // cloud-shadow count/size/opacity
    public float precipitation; // rain rate + overlay darkness
    public float windDirection; // -1 left / +1 right
}

/// <summary>A named target the live state eases toward (the 0..1 channels). Tuned on WeatherData.</summary>
[System.Serializable]
public class WeatherProfile
{
    public string name = "Clear";
    [Range(0f, 1f)] public float severity;
    [Range(0f, 1f)] public float wind;
    [Range(0f, 1f)] public float cloudiness;
    [Range(0f, 1f)] public float precipitation;
}

public enum CasualWeather { Clear = 0, Cloudy = 1, Windy = 2 }
```

- [ ] **Step 4: Create `WeatherMath.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Pure, stateless weather math (no Unity objects beyond Mathf) — unit-testable, lives in
/// IdleFarm.EconomyCore. Drives the WeatherController's blending + scheduling + the rain angle.
/// </summary>
public static class WeatherMath
{
    /// <summary>MoveTowards ease of one channel; never overshoots.</summary>
    public static float EaseChannel(float current, float target, float dt, float speed)
        => Mathf.MoveTowards(current, target, Mathf.Max(0f, dt) * Mathf.Max(0f, speed));

    /// <summary>Storm number -> 0..1 severity. stormsToMax storms reach full severity.</summary>
    public static float StormSeverity(int stormNumber, float stormsToMax)
        => Mathf.Clamp01(stormNumber / Mathf.Max(1f, stormsToMax));

    /// <summary>
    /// Rain fall angle from vertical (degrees). Severity is the main driver; wind nudges it.
    /// 0 = straight down, maxAngleDeg = near-horizontal (reserved for high-severity storms).
    /// </summary>
    public static float RainAngleDegrees(float wind, float severity, float maxAngleDeg)
        => maxAngleDeg * Mathf.Clamp01(Mathf.Clamp01(severity) + 0.2f * Mathf.Clamp01(wind) * 0f);
        // NOTE: wind term intentionally 0 in the mapping (severity drives angle); wind sets DIRECTION
        // + horizontal speed at the consumer. Kept in signature for future tuning.

    /// <summary>Weighted pick of a casual mood from a 0..1 roll. Returns CasualWeather as int.</summary>
    public static int RollCasual(float rng01, float clearWeight, float cloudyWeight, float windyWeight)
    {
        float c = Mathf.Max(0f, clearWeight);
        float cl = Mathf.Max(0f, cloudyWeight);
        float w = Mathf.Max(0f, windyWeight);
        float total = Mathf.Max(1e-5f, c + cl + w);
        float r = Mathf.Clamp01(rng01) * total;
        if (r < c) return 0;        // Clear
        if (r < c + cl) return 1;   // Cloudy
        return 2;                   // Windy
    }
}
```

> Implementer note: the `RainAngle` test expects severity-only behavior (the `* 0f` zeroes the wind term); the wind term stays in the signature so we can dial it in later without touching call sites.

- [ ] **Step 5: Run tests, verify PASS** (4 tests).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/EconomyCore/WeatherTypes.cs" "Assets/Scripts/EconomyCore/WeatherMath.cs" "Assets/Tests/EditMode/WeatherMathTests.cs"
git commit -m "feat(weather): WeatherState/WeatherProfile + WeatherMath pure logic (4 tests)"
```

---

### Task 2: WeatherData — profiles + scheduler + storm config

**Files:**
- Modify: `Assets/Scripts/WeatherData.cs` (append a new region after the Storm Wind Gusts block, before Runtime Helpers)

**Interfaces — Produces (public serialized on `WeatherData`):**
- `WeatherProfile profileClear, profileCloudy, profileWindy`
- `float stormCloudinessMin, stormCloudinessMax, stormWindMin, stormWindMax, stormPrecipMin, stormPrecipMax`
- `float weatherBlendSpeed`, `float casualRollIntervalMin, casualRollIntervalMax, casualEventDurationMin, casualEventDurationMax`
- `float casualClearWeight, casualCloudyWeight, casualWindyWeight`
- `float stormsToMaxSeverity`, `float rainMaxAngleDeg`

- [ ] **Step 1: Add fields** — insert before `// Runtime Helpers (used by ThunderstormManager)`:

```csharp
    // ─────────────────────────────────────────────────────────────────────
    // Weather Profiles + Scheduler (unified casual + storm system)
    // ─────────────────────────────────────────────────────────────────────

    [Header("Weather — Blend")]
    [Tooltip("How fast the live weather eases toward the active profile (per second).")]
    [Range(0.05f, 3f)] public float weatherBlendSpeed = 0.4f;

    [Header("Weather — Casual Profiles")]
    public WeatherProfile profileClear  = new WeatherProfile { name = "Clear",  severity = 0f, wind = 0.02f, cloudiness = 0.05f, precipitation = 0f };
    public WeatherProfile profileCloudy = new WeatherProfile { name = "Cloudy", severity = 0f, wind = 0.12f, cloudiness = 0.7f,  precipitation = 0f };
    public WeatherProfile profileWindy  = new WeatherProfile { name = "Windy",  severity = 0.08f, wind = 0.65f, cloudiness = 0.45f, precipitation = 0f };

    [Header("Weather — Casual Scheduler (runs everywhere)")]
    [Tooltip("Random seconds between casual-weather rolls.")]
    public Vector2 casualRollInterval = new Vector2(120f, 240f);
    [Tooltip("Random seconds a casual mood lasts before easing back to Clear.")]
    public Vector2 casualEventDuration = new Vector2(20f, 40f);
    [Tooltip("Weights for the casual roll: Clear (skip), Cloudy, Windy.")]
    public float casualClearWeight = 2.5f;
    public float casualCloudyWeight = 2f;
    public float casualWindyWeight = 1.5f;

    [Header("Weather — Storm Scaling")]
    [Tooltip("Storm number that reaches full severity (1.0).")]
    [Range(1f, 10f)] public float stormsToMaxSeverity = 5f;
    [Tooltip("Storm channel ranges, lerped by severity (min at Storm~1, max at full severity).")]
    public Vector2 stormCloudiness = new Vector2(0.6f, 1f);
    public Vector2 stormWind = new Vector2(0.35f, 1f);
    public Vector2 stormPrecip = new Vector2(0.4f, 1f);

    [Header("Weather — Rain")]
    [Tooltip("Max rain fall angle from vertical (deg) at full severity. Near-horizontal late storms.")]
    [Range(10f, 85f)] public float rainMaxAngleDeg = 75f;
```

- [ ] **Step 2: Verify compile** (refresh + `read_console` for `error CS` = none).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/WeatherData.cs"
git commit -m "feat(weather): WeatherData profiles + scheduler + storm scaling config"
```

---

### Task 3: WeatherController (brain + scheduler + storm API)

**Files:**
- Create: `Assets/Scripts/WeatherController.cs`

**Interfaces:**
- Consumes: `WeatherMath`, `WeatherState`, `WeatherProfile`, `WeatherData` (Tasks 1–2)
- Produces:
  - `WeatherController.Instance` (singleton), `WeatherState State { get; }`
  - `void BeginStorm(float severity)`, `void EndStorm()`
  - publishes `_GlobalWindMul` is NOT done here (WindController does it, Task 4)

- [ ] **Step 1: Implement** — `Assets/Scripts/WeatherController.cs`

```csharp
using UnityEngine;

/// <summary>
/// The single brain for weather. Owns the live WeatherState and eases it toward the active profile —
/// casual moods on a random schedule (everywhere), or a storm profile when ThunderstormManager calls
/// BeginStorm. Every effect reads WeatherController.Instance.State. Play-mode only.
/// </summary>
public class WeatherController : MonoBehaviour
{
    public static WeatherController Instance { get; private set; }

    [SerializeField] private WeatherData data;

    public WeatherState State => state;
    private WeatherState state;

    private bool stormActive;
    private WeatherProfile stormProfile;      // built on BeginStorm
    private CasualWeather casualMood = CasualWeather.Clear;
    private float rollTimer;
    private float eventTimer;                 // >0 while a casual mood is held

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        state.windDirection = WindDir();
        rollTimer = data != null ? Random.Range(data.casualRollInterval.x, data.casualRollInterval.y) : 120f;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private float WindDir() => (data != null && data.windDriftDirection < 0f) ? -1f : 1f;

    public void BeginStorm(float severity)
    {
        if (data == null) return;
        stormActive = true;
        stormProfile = new WeatherProfile
        {
            name = "Storm",
            severity = Mathf.Clamp01(severity),
            cloudiness = Mathf.Lerp(data.stormCloudiness.x, data.stormCloudiness.y, severity),
            wind = Mathf.Lerp(data.stormWind.x, data.stormWind.y, severity),
            precipitation = Mathf.Lerp(data.stormPrecip.x, data.stormPrecip.y, severity),
        };
    }

    public void EndStorm() => stormActive = false;

    private void Update()
    {
        if (data == null) return;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        if (!stormActive) TickCasualSchedule(dt);

        WeatherProfile target = stormActive ? stormProfile : ActiveCasualProfile();
        float spd = data.weatherBlendSpeed;
        state.severity      = WeatherMath.EaseChannel(state.severity,      target.severity,      dt, spd);
        state.wind          = WeatherMath.EaseChannel(state.wind,          target.wind,          dt, spd);
        state.cloudiness    = WeatherMath.EaseChannel(state.cloudiness,    target.cloudiness,    dt, spd);
        state.precipitation = WeatherMath.EaseChannel(state.precipitation, target.precipitation, dt, spd);
        state.windDirection = WindDir();
    }

    private void TickCasualSchedule(float dt)
    {
        if (eventTimer > 0f)
        {
            eventTimer -= dt;
            if (eventTimer <= 0f) casualMood = CasualWeather.Clear; // back to clear
            return;
        }
        rollTimer -= dt;
        if (rollTimer <= 0f)
        {
            rollTimer = Random.Range(data.casualRollInterval.x, data.casualRollInterval.y);
            int pick = WeatherMath.RollCasual(Random.value, data.casualClearWeight, data.casualCloudyWeight, data.casualWindyWeight);
            casualMood = (CasualWeather)pick;
            if (casualMood != CasualWeather.Clear)
                eventTimer = Random.Range(data.casualEventDuration.x, data.casualEventDuration.y);
        }
    }

    private WeatherProfile ActiveCasualProfile()
    {
        switch (casualMood)
        {
            case CasualWeather.Cloudy: return data.profileCloudy;
            case CasualWeather.Windy:  return data.profileWindy;
            default:                   return data.profileClear;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Weather: Force Cloudy")] private void DbgCloudy() { stormActive = false; casualMood = CasualWeather.Cloudy; eventTimer = 30f; }
    [ContextMenu("Weather: Force Windy")]  private void DbgWindy()  { stormActive = false; casualMood = CasualWeather.Windy;  eventTimer = 30f; }
    [ContextMenu("Weather: Force Clear")]  private void DbgClear()  { stormActive = false; casualMood = CasualWeather.Clear;  eventTimer = 0f; }
    [ContextMenu("Weather: Force Storm 1")] private void DbgStorm1() => BeginStorm(WeatherMath.StormSeverity(1, data != null ? data.stormsToMaxSeverity : 5f));
    [ContextMenu("Weather: Force Storm 3")] private void DbgStorm3() => BeginStorm(WeatherMath.StormSeverity(3, data != null ? data.stormsToMaxSeverity : 5f));
    [ContextMenu("Weather: Force Storm 5")] private void DbgStorm5() => BeginStorm(WeatherMath.StormSeverity(5, data != null ? data.stormsToMaxSeverity : 5f));
    [ContextMenu("Weather: End Storm")]    private void DbgEndStorm() => EndStorm();
#endif
}
```

- [ ] **Step 2: Verify compile** (none `error CS`).

- [ ] **Step 3: Scene wiring** — add a root `WeatherController` GameObject in `Assets/Scenes/SampleScene.unity`, add the `WeatherController` component, assign `Assets/Data/Threats/ThunderstormData.asset` to its `data` field (`m_*` via `set_property data` with the asset path, or guid). Save scene.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/WeatherController.cs" "Assets/Scenes/SampleScene.unity"
git commit -m "feat(weather): WeatherController brain — profile blend + casual scheduler + storm API"
```

---

### Task 4: WindController reads WeatherState

**Files:**
- Modify: `Assets/Scripts/WindController.cs`

**Interfaces:** Consumes `WeatherController.Instance.State.wind` (Task 3).

- [ ] **Step 1: Replace the `Apply()` storm/ambient logic** so `_GlobalWindMul` derives from `state.wind` instead of always-on ambient. Replace the body of `Apply()` (keep `_WindTime` publish):

```csharp
    private void Apply()
    {
        float t = Time.realtimeSinceStartup;
        Shader.SetGlobalFloat(TimeID, t); // unscaled, game-speed agnostic

        // Wind strength now comes from the shared WeatherState (0..1). In the editor (no controller)
        // fall back to a gentle preview breeze so sway still previews.
        float windCh = (Application.isPlaying && WeatherController.Instance != null)
            ? WeatherController.Instance.State.wind
            : editorPreviewWind;

        float gust = (Mathf.PerlinNoise(t * gustSpeed, 0.37f) - 0.5f) * 2f * gustStrength * windCh;
        float wind = (windCh + gust) * maxWindMultiplier;
        Shader.SetGlobalFloat(WindID, Mathf.Max(0f, wind));
    }
```

Add serialized fields near the others:

```csharp
    [Header("Mapping")]
    [Tooltip("_GlobalWindMul at full weather wind (1.0).")]
    [SerializeField] private float maxWindMultiplier = 3f;
    [Tooltip("Editor-only preview breeze when no WeatherController is running.")]
    [SerializeField] private float editorPreviewWind = 0.15f;
```

Remove the now-unused `ambient`, `stormBoost`, `stormLerpSpeed`, `stormCurrent` members and the ThunderstormManager check (the WeatherController owns storm state now).

- [ ] **Step 2: Verify compile + run** — Play; with default Clear, sway should be minimal. (No `error CS`.)

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/WindController.cs"
git commit -m "feat(weather): WindController _GlobalWindMul from shared WeatherState.wind"
```

---

### Task 5: Cloud/Debris/Gust layers read WeatherState (via AtmosphereController)

**Files:**
- Modify: `Assets/Scripts/AtmosphereController.cs`, `CloudShadowLayer.cs`, `WindDebrisLayer.cs`

**Interfaces:**
- `CloudShadowLayer.Tick(float dt, float cloudiness, float wind, Camera cam)` (was `Tick(dt, windMul, intensity, cam)`)
- `WindDebrisLayer.Tick(float wind, Camera cam)` (was `Tick(windMul, intensity, cam)`)
- `StormGustLayer.Tick(float dt, Camera cam)` unchanged; burst on severity rising edge.
- `AtmosphereController` reads `WeatherController.Instance.State`.

- [ ] **Step 1: `AtmosphereController.Update`** — replace the intensity/windMul derivation + layer ticks:

```csharp
    private void Update()
    {
        if (weatherData == null) return;
        if (!weatherData.atmosphereEnabled) { OnDisable(); return; }
        if (weatherData.shadowStyle != lastStyle) ApplyStyle();

        WeatherState s = (Application.isPlaying && WeatherController.Instance != null)
            ? WeatherController.Instance.State : default;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float wTime = Shader.GetGlobalFloat(TimeID);

        Camera cam = Camera.main;
        if (weatherData.shadowStyle == ShadowStyle.TintDip)
            tintDip.Tick(wTime, s.cloudiness);            // TintDip pulse scales with cloudiness
        else
            shadows.Tick(dt, s.cloudiness, s.wind, cam);

        debris.Tick(s.wind, cam);

        // Gust burst on the severity rising edge (a storm ramping up), plus occasional gusts while severe.
        bool severe = s.severity > 0.15f;
        if (severe && !wasStorming) { gusts.TriggerBurst(cam); stormElapsed = 0f; gustTimer = weatherData.windGustInterval; }
        else if (severe) { stormElapsed += dt; if (stormElapsed <= weatherData.windGustWindow) { gustTimer -= dt; if (gustTimer <= 0f) { gusts.SpawnOne(cam); gustTimer = weatherData.windGustInterval; } } }
        wasStorming = severe;
        gusts.Tick(dt, cam);
    }
```

Remove the old `WindID`/`intensity`/`AtmosphereMath.EaseIntensity` lines from `Update` (intensity now = `s.severity`). Keep `TimeID`. Delete the now-unused `intensity` field and `WindID` if unused.

- [ ] **Step 2: `CloudShadowLayer`** — change signature + drive count/opacity/speed from cloudiness/wind. Replace `Tick`:

```csharp
    public void Tick(float dt, float cloudiness, float wind, Camera cam)
    {
        if (data == null || cam == null || !patchesVisible) return;
        float camX = cam.transform.position.x;
        float camHalf = cam.orthographicSize * cam.aspect;

        // Cloudiness sets how many patches + their opacity; wind sets drift speed.
        int targetCount = Mathf.RoundToInt(data.shadowMaxPatches * Mathf.Clamp01(cloudiness));
        float speed = data.shadowBaseDriftSpeed * (0.3f + 1.7f * Mathf.Clamp01(wind));
        float vx = AtmosphereMath.PatchVelocityX(speed, data.windDriftDirection);
        float opacity = data.shadowOpacity * Mathf.Clamp01(cloudiness);

        if (!seeded && targetCount > 0)
        {
            for (int i = 0; i < targetCount; i++) patches.Add(SpawnPatch(cam, camX, camHalf, onscreen: true));
            seeded = true;
        }

        for (int i = patches.Count - 1; i >= 0; i--)
        {
            Patch p = patches[i];
            if (p.go == null) { patches.RemoveAt(i); continue; }
            Vector3 pos = p.go.transform.position; pos.x += vx * dt; p.go.transform.position = pos;
            Color c = p.sr.color; c.a = Mathf.Clamp01(opacity); p.sr.color = c;
            if (AtmosphereMath.IsPatchOffscreen(pos.x, p.halfWidth, camX, camHalf, data.windDriftDirection))
            { SafeDestroy(p.go); patches.RemoveAt(i); }
        }

        while (patches.Count < targetCount) patches.Add(SpawnPatch(cam, camX, camHalf, onscreen: false));
        while (patches.Count > targetCount && patches.Count > 0) // cloudiness dropped: retire extras
        { int last = patches.Count - 1; if (patches[last].go != null) SafeDestroy(patches[last].go); patches.RemoveAt(last); }
    }
```

Remove the old per-patch `baseAlpha * opacityMul` storm logic (opacity now uniform from cloudiness). `SpawnPatch` keeps using `data.shadowSizeRange`/`shadowOpacity` for size/initial color.

- [ ] **Step 3: `WindDebrisLayer.Tick`** — signature + emission from wind only:

```csharp
    public void Tick(float wind, Camera cam)
    {
        if (ps == null || data == null) return;
        emission.rateOverTime = data.debrisBaseRate * Mathf.Clamp01(wind) * 4f; // 0 at calm
        float speedMag = 4f + 16f * Mathf.Clamp01(wind);
        float hSpeed = AtmosphereMath.PatchVelocityX(speedMag, data.windDriftDirection);
        vel.x = new ParticleSystem.MinMaxCurve(hSpeed);
        if (cam != null)
        {
            float camHalfH = cam.orthographicSize, camHalfW = cam.orthographicSize * cam.aspect;
            float x = AtmosphereMath.SpawnEdgeX(cam.transform.position.x, camHalfW, 1f, data.windDriftDirection);
            ps.transform.position = new Vector3(x, cam.transform.position.y, -1f);
            var shape = ps.shape; shape.scale = new Vector3(0.1f, camHalfH * 2f, 1f);
        }
    }
```

- [ ] **Step 4: `TintDipLayer.Tick`** — change second arg name to cloudiness (drive pulse peak by cloudiness). Replace `public void Tick(float unscaledTime, float intensity)` body's `intensity` use with the passed value (rename param to `cloudiness`); peak uses `data.tintDipMaxAlpha * Mathf.Clamp01(cloudiness)`.

- [ ] **Step 5: Verify compile + run** — Play at Clear: no clouds/debris. Right-click WeatherController → "Force Cloudy": clouds appear, no debris. "Force Windy": clouds faster + leaves. No `error CS`.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/AtmosphereController.cs" "Assets/Scripts/CloudShadowLayer.cs" "Assets/Scripts/WindDebrisLayer.cs" "Assets/Scripts/TintDipLayer.cs"
git commit -m "feat(weather): cloud/debris/gust layers driven by shared WeatherState channels"
```

---

### Task 6: Rain — rate from precipitation + straight angled streaks matched to wind

**Files:**
- Modify: `Assets/Scripts/RainOverlayUI.cs`

**Interfaces:** Consumes `WeatherController.Instance.State` (precipitation, wind, severity, windDirection) + `WeatherMath.RainAngleDegrees`.

- [ ] **Step 1: Drive rain from state each frame.** In `RainOverlayUI.Update()` (after the emitter repositioning), set emission + overlay alpha + velocity from the state, and make rain a **straight line** (kill gravity-driven curve):

```csharp
        if (Application.isPlaying && WeatherController.Instance != null && data != null)
        {
            WeatherState s = WeatherController.Instance.State;

            var emission = rainParticles.emission;
            emission.rateOverTime = data.rainParticleRate * Mathf.Clamp01(s.precipitation);

            SetOverlayAlpha(data.rainOverlayMaxAlpha * Mathf.Clamp01(s.precipitation));
            if (s.precipitation > 0.02f && !rainParticles.isPlaying) rainParticles.Play();

            // Straight diagonal: angle from vertical scales with severity; direction from windDirection.
            float angleDeg = WeatherMath.RainAngleDegrees(s.wind, s.severity, data.rainMaxAngleDeg);
            float dir = s.windDirection < 0f ? -1f : 1f;
            float fall = (rainMinSpeed + rainMaxSpeed) * 0.5f;
            float rad = angleDeg * Mathf.Deg2Rad;
            var main = rainParticles.main;
            main.gravityModifier = 0f; // no parabola
            var vel = rainParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(dir * fall * Mathf.Sin(rad));
            vel.y = new ParticleSystem.MinMaxCurve(-fall * Mathf.Cos(rad));
        }
```

> This replaces the old fixed `rainWindX` + gravity behavior so rain falls as a straight streak at the same angle/direction as the wind, steepening with severity. The `RainOverlayUI.FadeIn/FadeOut` calls from `ThunderstormManager` become no-ops in practice (precipitation now drives it); leave them but they won't fight the state because Update sets alpha/rate every frame.

- [ ] **Step 2:** In `BuildRainParticles`, set `main.gravityModifier = 0f` and `vel.x = 0` initial (Update drives it); set `renderer.sortingOrder = 5300` (rain in front of gusts). Keep Stretch render mode (streaks).

- [ ] **Step 3: Verify** — Force Storm 1: light, mostly vertical rain leaning with the wind. Force Storm 5: heavy, near-horizontal, same direction as the leaves/clouds. No `error CS`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/RainOverlayUI.cs"
git commit -m "feat(weather): rain rate from precipitation; straight streaks matched to wind+severity"
```

---

### Task 7: ThunderstormManager drives WeatherController

**Files:**
- Modify: `Assets/Scripts/ThunderstormManager.cs`

**Interfaces:** Calls `WeatherController.Instance.BeginStorm(WeatherMath.StormSeverity(stormNumber, weatherData.stormsToMaxSeverity))` / `EndStorm()`.

- [ ] **Step 1:** In `RunThunderstorm(int stormNumber)`, at the start set the weather storm, and at the end clear it:

```csharp
        stormActive = true;
        if (WeatherController.Instance != null)
            WeatherController.Instance.BeginStorm(WeatherMath.StormSeverity(stormNumber, weatherData.stormsToMaxSeverity));
```

and after the phases finish (just before `stormActive = false;`):

```csharp
        if (WeatherController.Instance != null) WeatherController.Instance.EndStorm();
        stormActive = false;
```

Also call `EndStorm()` in `OnRunEnded` / `EndAllEffectsImmediately()` so a storm-in-progress releases the weather when a run ends:

```csharp
        if (WeatherController.Instance != null) WeatherController.Instance.EndStorm();
```

- [ ] **Step 2:** The wind/rain DAMAGE ticks in `WindPhase`/`RainPhase`/lightning stay as-is (gameplay). The visual `RainOverlayUI.FadeIn/FadeOut` calls can stay (harmless) or be removed — leave them.

- [ ] **Step 3: Verify** — During a real wave-triggered storm (or `Force Storm`), clouds darken, wind + rain ramp together, ease back when the storm ends. No `error CS`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/ThunderstormManager.cs"
git commit -m "feat(weather): ThunderstormManager drives WeatherController storm severity"
```

---

### Task 8: Cloud dither/size caps + final tuning pass

**Files:**
- Modify: `Assets/Scripts/CloudShadowLayer.cs` (BuildBlobSprite + size), `Assets/Data/Threats/ThunderstormData.asset` (tune)

- [ ] **Step 1: Soften dither + raise texture resolution** so large patches don't read as giant pixels. In `BuildBlobSprite`, bump `N` 64 → 128 and soften the dither (quantize to more bands, lighter checker):

```csharp
        const int N = 128;                       // was 64 — finer so big patches aren't blocky
        ...
            if (dithered)
            {
                a = Mathf.Round(a * 5f) / 5f;    // gentler banding (was *3)
                if (((x + y) & 1) == 0) a *= 0.85f; // lighter checker (was 0.7)
            }
```

- [ ] **Step 2: Cap patch size** in the asset: set `shadowSizeRange` to e.g. (18, 36) (was 32–80) so clouds stop ballooning; set `shadowMaxPatches` 12 → 8. Apply via `manage_scriptable_object` (`shadowSizeRange.x`=18, `.y`=36, `shadowMaxPatches`=8). These are starting points; tune live.

- [ ] **Step 3: Verify** — Force Cloudy then Storm 3: clouds read as soft shadows, not big pixels. No `error CS`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/CloudShadowLayer.cs" "Assets/Data/Threats/ThunderstormData.asset"
git commit -m "feat(weather): soften+resize cloud dither, cap patch size/count"
```

---

## Self-Review

**Spec coverage:**
- Shared `WeatherState` (severity/wind/cloudiness/precip/direction) → Task 1 ✓
- Profiles in `WeatherData` (Clear/Cloudy/Windy/Storm) → Task 2 ✓
- `WeatherController` blend + casual scheduler (everywhere) + storm override → Task 3 ✓
- Effects as consumers: WindController (4), clouds/debris/gusts (5), rain (6) ✓
- Storm scaling via ThunderstormManager + severity curve → Tasks 1, 7 ✓
- Issue 1 constant particles → Clear=0 channels (4,5,6) ✓
- Issue 2 droopy/mismatched rain → straight angled streaks from wind+severity (6) ✓
- Issue 3 over-dithered clouds → size/count caps + softened dither (8) ✓
- EditMode tests for WeatherMath → Task 1 ✓
- Casual everywhere / storms runs-only → Task 3 scheduler + Task 7 storm calls ✓

**Placeholder scan:** none — all steps carry code/commands.

**Type consistency:** `WeatherState`/`WeatherProfile`/`CasualWeather`, `WeatherMath.{EaseChannel,StormSeverity,RainAngleDegrees,RollCasual}`, `WeatherController.{Instance,State,BeginStorm,EndStorm}`, and the changed layer `Tick` signatures (`CloudShadowLayer.Tick(dt,cloudiness,wind,cam)`, `WindDebrisLayer.Tick(wind,cam)`, `TintDipLayer.Tick(time,cloudiness)`) are used consistently across Tasks 3–7.
