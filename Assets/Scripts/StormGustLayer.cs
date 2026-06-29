using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Occasional wind-streak "gusts" that sweep across the screen at the start of a storm. Uses the
/// real Wind streak sprites (WeatherData.windStreakSprites). Each gust enters from the upwind edge,
/// flies across in windDriftDirection (same direction as clouds/leaves), and fades out. Owned by
/// AtmosphereController; cosmetic only. Sits above the other weather so gusts read in front.
/// </summary>
public class StormGustLayer
{
    private const int GustSortingOrder = 5200; // in front of cloud shadows (5000) + debris (5100), below lightning (6000)

    private class Gust
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float life;
        public float maxLife;
        public float vx;
    }

    private readonly Transform parent;
    private WeatherData data;
    private readonly List<Gust> gusts = new List<Gust>();

    public StormGustLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d) { data = d; }

    private bool HasSprites => data != null && data.windStreakSprites != null && data.windStreakSprites.Length > 0;

    /// <summary>Spawn a flurry of gusts (a storm just kicked off).</summary>
    public void TriggerBurst(Camera cam)
    {
        if (!HasSprites || cam == null) return;
        int n = Mathf.Max(1, data.windGustBurstCount);
        for (int i = 0; i < n; i++) SpawnGust(cam);
    }

    /// <summary>Spawn a single gust ("here and there" during the early storm).</summary>
    public void SpawnOne(Camera cam)
    {
        if (!HasSprites || cam == null) return;
        SpawnGust(cam);
    }

    private void SpawnGust(Camera cam)
    {
        Sprite s = data.windStreakSprites[Random.Range(0, data.windStreakSprites.Length)];
        if (s == null) return;

        float camHalfW = cam.orthographicSize * cam.aspect;
        float camHalfH = cam.orthographicSize;
        float camX = cam.transform.position.x;
        float camY = cam.transform.position.y;

        var go = new GameObject("WindGust");
        go.transform.SetParent(parent, false);

        float scale = Random.Range(data.windGustScaleRange.x, data.windGustScaleRange.y);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        // Enter from the upwind edge; spread the burst out further upwind so they trail in over time.
        float edge = AtmosphereMath.SpawnEdgeX(camX, camHalfW, 0f, data.windDriftDirection);
        float backOff = Random.Range(0f, camHalfW); // further off-screen on the upwind side
        float x = edge + (data.windDriftDirection < 0f ? backOff : -backOff);
        float y = camY + Random.Range(-camHalfH * 0.8f, camHalfH * 0.9f);
        go.transform.position = new Vector3(x, y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = GustSortingOrder;
        sr.flipX = data.windDriftDirection > 0f; // streaks point the way the wind blows
        sr.color = new Color(1f, 1f, 1f, data.windGustAlpha);

        float speed = data.windGustSpeed * Random.Range(0.8f, 1.2f);
        gusts.Add(new Gust
        {
            go = go,
            sr = sr,
            life = 0f,
            maxLife = data.windGustLife,
            vx = AtmosphereMath.PatchVelocityX(speed, data.windDriftDirection)
        });
    }

    public void Tick(float dt, Camera cam)
    {
        for (int i = gusts.Count - 1; i >= 0; i--)
        {
            Gust g = gusts[i];
            if (g.go == null) { gusts.RemoveAt(i); continue; }

            g.life += dt;
            Vector3 pos = g.go.transform.position;
            pos.x += g.vx * dt;
            g.go.transform.position = pos;

            // Quick ease-in, slow fade-out over the life.
            float t = Mathf.Clamp01(g.life / Mathf.Max(0.01f, g.maxLife));
            float fade = t < 0.15f ? (t / 0.15f) : (1f - (t - 0.15f) / 0.85f);
            Color c = g.sr.color;
            c.a = data.windGustAlpha * Mathf.Clamp01(fade);
            g.sr.color = c;

            bool off = cam != null &&
                       AtmosphereMath.IsPatchOffscreen(pos.x, 0f, cam.transform.position.x,
                                                       cam.orthographicSize * cam.aspect, data.windDriftDirection);
            if (g.life >= g.maxLife || off)
            {
                SafeDestroy(g.go);
                gusts.RemoveAt(i);
            }
        }
    }

    public void Clear()
    {
        foreach (var g in gusts) if (g.go != null) SafeDestroy(g.go);
        gusts.Clear();
    }

    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }
}
