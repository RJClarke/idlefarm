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

    /// <summary>Continuous -1..+1 wind direction from WeatherState (eases through 0 when it turns).</summary>
    private float windDirSigned = -1f;

    public WindDebrisLayer(Transform parent) { this.parent = parent; }

    public void Configure(WeatherData d)
    {
        data = d;
        windDirSigned = (d != null && d.windDriftDirection < 0f) ? -1f : 1f;
        if (ps == null) Build();
    }

    public void SetWindDirection(float signed) => windDirSigned = Mathf.Clamp(signed, -1f, 1f);

    public void SetActive(bool active)
    {
        if (ps == null) return;
        if (active) { if (!ps.isPlaying) ps.Play(); }
        else { ps.Stop(); ps.Clear(); }
    }

    /// <summary>
    /// Drive the leaves off the live weather. Leaves travel on a straight line at an angle from
    /// vertical: they lie down toward horizontal as the wind rises, but while rain is falling they
    /// swing back toward the rain's own angle (and slow down) so the two layers agree.
    /// </summary>
    public void Tick(float wind, float precipitation, float rainAngleDeg, Camera cam)
    {
        if (ps == null || data == null) return;

        wind = Mathf.Clamp01(wind);
        emission.rateOverTime = data.debrisBaseRate * wind * 4f; // ~0 at calm

        float speed = WeatherMath.DebrisSpeed(wind, precipitation,
                                              data.debrisSpeedRange.x, data.debrisSpeedRange.y,
                                              data.debrisRainSpeedMul);
        float angleDeg = WeatherMath.DebrisAngleDegrees(wind, precipitation, rainAngleDeg,
                                                        data.debrisCalmAngleDeg, data.debrisWindyAngleDeg,
                                                        data.debrisRainMatchOffsetDeg);
        float rad = angleDeg * Mathf.Deg2Rad;

        // Continuous direction so a turning wind eases through a lull instead of snapping sides.
        vel.x = new ParticleSystem.MinMaxCurve(AtmosphereMath.SignedDriftX(speed * Mathf.Sin(rad), windDirSigned));
        vel.y = new ParticleSystem.MinMaxCurve(-speed * Mathf.Cos(rad));

        if (cam != null)
        {
            // Leaves now fall as well as blow, so they stream in from above like the rain does
            // (a horizontal line offset upwind and widened by the sideways drift) rather than from
            // a vertical slot at the screen edge — that only ever covered a diagonal band.
            float camHalfH = cam.orthographicSize;
            float camW = cam.orthographicSize * cam.aspect * 2f;
            Vector3 camPos = cam.transform.position;

            float vx = vel.x.constant;
            float avgLife = 5.5f; // matches startLifetime 4..7
            float drift = Mathf.Abs(vx) * avgLife;
            float dirSign = windDirSigned < 0f ? -1f : 1f;

            ps.transform.position = new Vector3(camPos.x - dirSign * drift * 0.5f, camPos.y + camHalfH + 2f, -1f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = new Vector3(camW + drift + 4f, 0.1f, 1f);
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
        shape.scale = new Vector3(40f, 0.1f, 1f); // Tick() corrects width + position every frame

        vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-4f);
        vel.y = new ParticleSystem.MinMaxCurve(-4f);

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
