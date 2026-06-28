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
    private const int ShadowSortingOrder = 200; // above ground tiles, below the ~1000+ entity band

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
    private Material mat;
    private bool patchesVisible = true;

    public CloudShadowLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        if (mat == null)          mat          = new Material(Shader.Find("Sprites/Default"));
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
            if (p.go == null) { patches.RemoveAt(i); continue; }

            Vector3 pos = p.go.transform.position;
            pos.x += -dir * speed * dt; // dir is the upwind sign; patches move downwind
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
        sr.material = mat;
        sr.sortingOrder = ShadowSortingOrder;
        sr.color = new Color(1f, 1f, 1f, data.shadowOpacity);

        return new Patch { go = go, sr = sr, halfWidth = half, baseAlpha = data.shadowOpacity };
    }

    public void Clear()
    {
        foreach (var p in patches) if (p.go != null) Object.Destroy(p.go);
        patches.Clear();
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
            float a = Mathf.Clamp01(1f - d);
            a = a * a; // softer edge
            if (dithered)
            {
                a = Mathf.Round(a * 4f) / 4f;        // quantize to 4 bands
                if (((x + y) & 1) == 0) a *= 0.6f;   // checker dither on alternating pixels
            }
            // Dark sprite (RGB near black), alpha is the mask. Drawn translucent → darkens scene.
            px[y * N + x] = new Color(0.05f, 0.06f, 0.10f, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }
}
