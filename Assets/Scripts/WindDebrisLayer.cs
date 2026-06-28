using UnityEngine;

/// <summary>
/// Visible wind: a sparse stream of tumbling leaves that thickens as the global wind
/// multiplier and storm intensity rise. Built like RainOverlayUI's particle system;
/// emitter tracks Camera.main. Cosmetic only.
/// </summary>
public class WindDebrisLayer
{
    private readonly Transform parent;
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

        emission.rateOverTime = AtmosphereMath.EmissionRate(data.debrisBaseRate, windMul, intensity, data.debrisStormRateMul);

        // Use the SAME direction math as the cloud shadows so everything blows the same way
        // off the global windDriftDirection (-1 left / +1 right).
        float speedMag = 4f + 10f * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity));
        float hSpeed = AtmosphereMath.PatchVelocityX(speedMag, data.windDriftDirection); // drifts WITH the wind
        vel.x = new ParticleSystem.MinMaxCurve(hSpeed);

        if (cam != null)
        {
            float camHalfH = cam.orthographicSize;
            float camHalfW = cam.orthographicSize * cam.aspect;
            float x = AtmosphereMath.SpawnEdgeX(camX: cam.transform.position.x, camHalfWidth: camHalfW,
                                                patchHalfWidth: 1f, windDirX: data.windDriftDirection); // upwind edge
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
        // Fresh foliage: random per-leaf between a leafy green and a yellow-green (not fall colors).
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.33f, 0.58f, 0.18f, 0.9f),   // green
            new Color(0.62f, 0.74f, 0.22f, 0.9f));  // yellow-green

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

        var rot = ps.rotationOverLifetime; // leaves tumble
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 5100; // above the cloud shadows + entities so leaves blow in front; below HUD
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        if (data.debrisSprites != null && data.debrisSprites.Length > 0)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            for (int i = 0; i < data.debrisSprites.Length; i++) tsa.AddSprite(data.debrisSprites[i]);
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f); // one sprite per particle
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, data.debrisSprites.Length);
        }

        ps.Stop();
    }
}
