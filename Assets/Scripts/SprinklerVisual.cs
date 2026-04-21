using UnityEngine;

/// <summary>
/// Generates a pixel-art sprinkler sprite at runtime and
/// displays a radar-ping AoE ring during the active watering phase.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SprinklerVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Sprite spriteOverride;
    [SerializeField] private int textureSize = 32;
    [SerializeField] private int sortingOrder = 10;

    [Header("Ping Settings")]
    [SerializeField] private float pingDuration = 1.0f;
    [SerializeField] private float pingInterval = 0.4f;
    [SerializeField] private Color pingColor = new Color(0.5f, 0.8f, 1f, 1f);
    [SerializeField] private float lineWidth = 0.12f;
    [SerializeField] private int segments = 48;

    private float aoERadius = 2f;
    private bool isPinging = false;
    private float pingTimer = 0f;

    private LineRenderer[] rings;
    private float[] ringTimers;
    private const int MAX_RINGS = 3;

    private void Awake()
    {
        if (spriteOverride != null)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            sr.sprite = spriteOverride;
            sr.sortingOrder = sortingOrder;
        }
        else
        {
            BuildSprinklerSprite();
        }
        InitRings();
    }

    private void InitRings()
    {
        rings = new LineRenderer[MAX_RINGS];
        ringTimers = new float[MAX_RINGS];

        for (int i = 0; i < MAX_RINGS; i++)
        {
            GameObject ringObj = new GameObject($"PingRing_{i}");
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = pingColor;
            lr.endColor = pingColor;
            lr.sortingOrder = sortingOrder - 1;
            lr.enabled = false;

            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                lr.SetPosition(s, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }

            rings[i] = lr;
            ringTimers[i] = -1f;
        }
    }

    public void SetRadius(float radius)
    {
        aoERadius = radius;
    }

    public void StartPinging()
    {
        if (isPinging) return;
        isPinging = true;
        pingTimer = 0f;
    }

    public void StopPinging()
    {
        isPinging = false;
    }

    private void Update()
    {
        if (isPinging)
        {
            pingTimer -= Time.unscaledDeltaTime;
            if (pingTimer <= 0f)
            {
                SpawnRing();
                pingTimer = pingInterval;
            }
        }

        for (int i = 0; i < MAX_RINGS; i++)
        {
            if (ringTimers[i] < 0f) continue;

            ringTimers[i] += Time.unscaledDeltaTime;
            float t = ringTimers[i] / pingDuration;

            if (t >= 1f)
            {
                rings[i].enabled = false;
                ringTimers[i] = -1f;
                continue;
            }

            float scale = Mathf.Lerp(0.1f, aoERadius, t);
            rings[i].transform.localScale = Vector3.one * scale;

            float alpha = pingColor.a * (1f - t);
            Color c = new Color(pingColor.r, pingColor.g, pingColor.b, alpha);
            rings[i].startColor = c;
            rings[i].endColor = c;
        }
    }

    private void SpawnRing()
    {
        for (int i = 0; i < MAX_RINGS; i++)
        {
            if (ringTimers[i] < 0f)
            {
                rings[i].enabled = true;
                rings[i].transform.localScale = Vector3.one * 0.1f;
                ringTimers[i] = 0f;
                return;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Placeholder Sprite Builder
    // ─────────────────────────────────────────────────────────────────────

    private void BuildSprinklerSprite()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0, 0, 0, 0);
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);

        Color pipe    = new Color32(100, 100, 110, 255);
        Color pipeHi  = new Color32(140, 140, 155, 255);
        Color base1   = new Color32(80, 80, 90, 255);
        Color base2   = new Color32(60, 60, 70, 255);
        Color nozzle  = new Color32(50, 130, 200, 255);
        Color water   = new Color32(100, 180, 240, 180);

        int cx = textureSize / 2;

        for (int x = cx - 5; x <= cx + 4; x++)
        {
            tex.SetPixel(x, 0, base2);
            tex.SetPixel(x, 1, base1);
        }
        for (int x = cx - 4; x <= cx + 3; x++)
        {
            tex.SetPixel(x, 2, base1);
            tex.SetPixel(x, 3, base1);
        }

        for (int y = 4; y <= 14; y++)
        {
            tex.SetPixel(cx - 1, y, pipe);
            tex.SetPixel(cx, y, pipeHi);
            tex.SetPixel(cx + 1, y, pipe);
        }

        for (int x = cx - 3; x <= cx + 2; x++)
        {
            tex.SetPixel(x, 15, pipe);
            tex.SetPixel(x, 16, pipeHi);
        }
        for (int x = cx - 2; x <= cx + 1; x++)
        {
            tex.SetPixel(x, 17, nozzle);
            tex.SetPixel(x, 18, nozzle);
        }

        tex.SetPixel(cx - 4, 20, water); tex.SetPixel(cx - 5, 22, water);
        tex.SetPixel(cx - 6, 24, water); tex.SetPixel(cx - 3, 21, water);
        tex.SetPixel(cx - 7, 26, water);
        tex.SetPixel(cx - 1, 20, water); tex.SetPixel(cx, 22, water);
        tex.SetPixel(cx, 24, water);     tex.SetPixel(cx + 1, 21, water);
        tex.SetPixel(cx - 1, 26, water); tex.SetPixel(cx + 1, 25, water);
        tex.SetPixel(cx + 3, 20, water); tex.SetPixel(cx + 4, 22, water);
        tex.SetPixel(cx + 5, 24, water); tex.SetPixel(cx + 2, 21, water);
        tex.SetPixel(cx + 6, 26, water);

        tex.Apply();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0f),
            textureSize);
        sr.sortingOrder = sortingOrder;
    }
}
