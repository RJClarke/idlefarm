using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Warm light rays drawn as wedges with Painter2D. Rotate the element via style.rotate.</summary>
public sealed class RaysElement : VisualElement
{
    public int rayCount = 12;
    public Color color = new Color(1f, 0.86f, 0.45f, 0.35f);

    public RaysElement()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    private void Draw(MeshGenerationContext ctx)
    {
        Rect r = contentRect;
        if (r.width <= 0f) return;
        Vector2 c = r.center;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;
        float half = Mathf.PI / rayCount * 0.45f;
        Painter2D p = ctx.painter2D;
        p.fillColor = color;
        for (int i = 0; i < rayCount; i++)
        {
            float a = i * Mathf.PI * 2f / rayCount;
            p.BeginPath();
            p.MoveTo(c);
            p.LineTo(c + new Vector2(Mathf.Cos(a - half), Mathf.Sin(a - half)) * radius);
            p.LineTo(c + new Vector2(Mathf.Cos(a + half), Mathf.Sin(a + half)) * radius);
            p.ClosePath();
            p.Fill();
        }
    }
}
