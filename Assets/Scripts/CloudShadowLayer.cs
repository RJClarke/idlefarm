using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a pool of world-space drifting cloud-shadow patches (Soft / Dithered styles).
/// Owned by AtmosphereController. Patches spawn off the upwind edge, drift across at a
/// wind-scaled speed, and recycle when fully off the downwind edge. Drawn as low-alpha dark
/// sprites so they darken whatever they pass over. Sorting sits above ground, below entities.
/// </summary>
public class CloudShadowLayer
{
    // Above the entity Y-sort band (~1000–3000) so the shadow falls OVER trees/crops/helpers/animals,
    // like a real cloud passing overhead. Below the lightning bolt (6000) and the ScreenSpaceOverlay HUD.
    private const int ShadowSortingOrder = 5000;

    private class Patch
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float halfWidth;
        public float baseAlpha;
    }

    private readonly Transform parent;
    private WeatherData data;
    private readonly List<Patch> patches = new List<Patch>();
    private Sprite softSprite;
    private Sprite ditherSprite;
    private bool patchesVisible = true;
    private bool seeded;

    public CloudShadowLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        if (softSprite == null)   softSprite   = BuildBlobSprite(false);
        if (ditherSprite == null) ditherSprite = BuildBlobSprite(true);
    }

    public void SetActiveStyle(bool visible)
    {
        patchesVisible = visible;
        Clear(); // reset so a toggle / style switch reseeds a fresh, distributed batch
    }

    public void Tick(float dt, float windMul, float intensity, Camera cam)
    {
        if (data == null || cam == null || !patchesVisible) return;

        float camX = cam.transform.position.x;
        float camHalf = cam.orthographicSize * cam.aspect;
        float speed = AtmosphereMath.DriftSpeed(data.shadowBaseDriftSpeed, windMul, intensity, data.shadowStormSpeedMul);
        float vx = AtmosphereMath.PatchVelocityX(speed, data.windDriftDirection); // clouds drift WITH the wind
        float opacityMul = 1f + Mathf.Clamp01(intensity) * data.shadowStormOpacityMul;

        // First fill: scatter patches across the visible area so shadows show up immediately
        // (rather than waiting ~15s for them to drift in from the edge).
        if (!seeded && data.shadowMaxPatches > 0)
        {
            for (int i = 0; i < data.shadowMaxPatches; i++)
                patches.Add(SpawnPatch(cam, camX, camHalf, onscreen: true));
            seeded = true;
        }

        // Drift + recycle existing.
        for (int i = patches.Count - 1; i >= 0; i--)
        {
            Patch p = patches[i];
            if (p.go == null) { patches.RemoveAt(i); continue; }

            Vector3 pos = p.go.transform.position;
            pos.x += vx * dt;
            p.go.transform.position = pos;

            Color c = p.sr.color;
            c.a = Mathf.Clamp01(p.baseAlpha * opacityMul);
            p.sr.color = c;

            if (AtmosphereMath.IsPatchOffscreen(pos.x, p.halfWidth, camX, camHalf, data.windDriftDirection))
            {
                SafeDestroy(p.go);
                patches.RemoveAt(i);
            }
        }

        // Steady-state: new patches enter from the upwind edge.
        while (patches.Count < data.shadowMaxPatches)
            patches.Add(SpawnPatch(cam, camX, camHalf, onscreen: false));
    }

    private Patch SpawnPatch(Camera cam, float camX, float camHalf, bool onscreen)
    {
        float width = Random.Range(data.shadowSizeRange.x, data.shadowSizeRange.y);
        float half = width * 0.5f;
        float camY = cam.transform.position.y;
        float camHalfH = cam.orthographicSize;

        var go = new GameObject("CloudShadowPatch");
        go.transform.SetParent(parent, false);
        float x = onscreen
            ? camX + Random.Range(-camHalf, camHalf)                          // scattered across the view
            : AtmosphereMath.SpawnEdgeX(camX, camHalf, half, data.windDriftDirection); // enter from upwind edge
        float y = camY + Random.Range(-camHalfH, camHalfH);
        go.transform.position = new Vector3(x, y, 0f);
        float aspect = Random.Range(0.5f, 0.8f); // wider than tall
        go.transform.localScale = new Vector3(width, width * aspect, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = data.shadowStyle == ShadowStyle.Dithered ? ditherSprite : softSprite;
        // No custom material — a fresh SpriteRenderer already has the project's default sprite
        // material (URP-correct). Forcing a built-in "Sprites/Default" material renders nothing
        // under URP's 2D Renderer (the lightning bolt in ThunderstormManager works the same way).
        sr.sortingOrder = ShadowSortingOrder;
        sr.color = new Color(1f, 1f, 1f, data.shadowOpacity);

        return new Patch { go = go, sr = sr, halfWidth = half, baseAlpha = data.shadowOpacity };
    }

    public void Clear()
    {
        foreach (var p in patches) if (p.go != null) SafeDestroy(p.go);
        patches.Clear();
        seeded = false;
    }

    // [ExecuteAlways] means we tick in edit mode too, where Object.Destroy is illegal.
    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }

    /// <summary>
    /// A radial blob: dark center fading to transparent edges. The dithered version quantizes the
    /// falloff into a few hard bands plus a checker pattern so it reads pixel-native at 32 PPU.
    /// The sprite uses 1 world unit = N px, so the patch's actual size comes from transform scale.
    /// </summary>
    private static Sprite BuildBlobSprite(bool dithered)
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            filterMode = dithered ? FilterMode.Point : FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[N * N];
        Vector2 c = new Vector2(N * 0.5f, N * 0.5f);
        float maxR = N * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxR; // 0 center .. 1 edge
            // Broad flat core (a≈1 out to ~45% radius) with a soft rim — reads as a shadow,
            // not a tiny dot. NOTE: Unity's Mathf.SmoothStep(from,to,t) interpolates *values*,
            // it is NOT the HLSL edge-threshold smoothstep — so build the plateau manually.
            const float core = 0.45f;                            // solid out to this fraction of radius
            float t = Mathf.Clamp01((1f - d) / (1f - core));     // 1 inside core → 0 at edge
            float a = t * t * (3f - 2f * t);                     // smoothstep falloff on the rim
            if (dithered)
            {
                a = Mathf.Round(a * 3f) / 3f;        // quantize to a few hard bands
                if (((x + y) & 1) == 0) a *= 0.7f;   // checker dither on alternating pixels
            }
            // Dark sprite (RGB near black), alpha is the mask. Drawn translucent → darkens scene.
            px[y * N + x] = new Color(0.05f, 0.06f, 0.10f, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }
}
