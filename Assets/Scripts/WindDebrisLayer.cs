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

        float dir = Mathf.Sign(data.windDriftDirection == 0f ? -1f : data.windDriftDirection);
        float hSpeed = -dir * (4f + 10f * Mathf.Max(0f, windMul) * (1f + Mathf.Clamp01(intensity)));
        vel.x = new ParticleSystem.MinMaxCurve(hSpeed);

        if (cam != null)
        {
            float camHalfH = cam.orthographicSize;
            float camHalfW = cam.orthographicSize * cam.aspect;
            float x = cam.transform.position.x + dir * (camHalfW + 1f); // emit from upwind edge
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

        var rot = ps.rotationOverLifetime; // leaves tumble
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
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f); // one sprite per particle
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, data.debrisSprites.Length);
        }

        ps.Stop();
    }
}
