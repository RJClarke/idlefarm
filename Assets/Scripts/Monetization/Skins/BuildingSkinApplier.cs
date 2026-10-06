using UnityEngine;

/// <summary>Swaps a building's sprite for its equipped skin, drawn at the base sprite's world WIDTH with the base
/// edge kept where it was (taller skins grow upward). Transform, collider and BarnBuilding's press tween are untouched.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BuildingSkinApplier : MonoBehaviour
{
    public static Sprite FarmhouseBase { get; private set; }

    private string target;
    private SpriteRenderer sr;
    private Sprite baseSprite;
    private Sprite fitted;
    private string fittedId;
    private bool subscribed;

    public void Init(string target)
    {
        this.target = target;
        sr = GetComponent<SpriteRenderer>();
        baseSprite = sr.sprite;
        if (target == "farmhouse") FarmhouseBase = baseSprite;
        TrySubscribe();
        Apply();
    }

    private void OnEnable() => TrySubscribe();

    private void TrySubscribe()
    {
        if (subscribed || SkinManager.Instance == null || target == null) return;
        SkinManager.Instance.OnSkinsChanged += Apply;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && SkinManager.Instance != null) SkinManager.Instance.OnSkinsChanged -= Apply;
        subscribed = false;
    }

    private void OnDestroy() { if (fitted != null) Destroy(fitted); }

    private void Apply()
    {
        if (sr == null || baseSprite == null) return;
        SkinDef def = SkinManager.Instance != null ? SkinManager.Instance.EquippedDef(target) : null;
        if (def == null || def.sprite == null) { sr.sprite = baseSprite; return; }
        if (fittedId != def.id)
        {
            if (fitted != null) Destroy(fitted);
            fitted = Fit(def.sprite);
            fittedId = def.id;
        }
        sr.sprite = fitted;
    }

    private Sprite Fit(Sprite skin)
    {
        float baseW = baseSprite.rect.width / baseSprite.pixelsPerUnit;
        float baseH = baseSprite.rect.height / baseSprite.pixelsPerUnit;
        float ppu = skin.rect.width / baseW;                 // same world width as the base
        float skinH = skin.rect.height / ppu;
        Vector2 basePivot = new Vector2(baseSprite.pivot.x / baseSprite.rect.width, baseSprite.pivot.y / baseSprite.rect.height);
        // Keep the bottom edge: base bottom sits basePivot.y*baseH below the pivot -> solve for the skin's pivot.y.
        Vector2 pivot = new Vector2(basePivot.x, basePivot.y * baseH / Mathf.Max(0.0001f, skinH));
        Sprite s = Sprite.Create(skin.texture, skin.rect, pivot, ppu, 0, SpriteMeshType.FullRect);
        s.name = skin.name + "_fitted";
        return s;
    }
}
