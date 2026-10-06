using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws an animal in its equipped colour variant. Variant sheets share the base sheet's exact pixel
/// layout, so each base frame maps to a sprite cut from the skin texture with the SAME rect, pivot and PPU.
/// Runs in LateUpdate, after the Animator has written this frame's sprite. Other textures (eggs, etc.) pass through.</summary>
public sealed class SkinSwapper : MonoBehaviour
{
    private string target;
    private SpriteRenderer sr;
    private Texture2D baseTexture;
    private Texture2D skinTexture;
    private readonly Dictionary<Sprite, Sprite> toSkin = new Dictionary<Sprite, Sprite>();
    private readonly Dictionary<Sprite, Sprite> toBase = new Dictionary<Sprite, Sprite>();
    private bool subscribed;

    public static void Attach(GameObject visual, string target)
    {
        if (visual == null || string.IsNullOrEmpty(target)) return;
        SkinSwapper s = visual.GetComponent<SkinSwapper>();
        if (s == null) s = visual.AddComponent<SkinSwapper>();
        s.target = target;
        s.sr = visual.GetComponentInChildren<SpriteRenderer>();
        s.baseTexture = s.sr != null && s.sr.sprite != null ? s.sr.sprite.texture : null;
        s.Refresh();
    }

    private void OnEnable() => TrySubscribe();

    private void TrySubscribe()
    {
        if (subscribed || SkinManager.Instance == null) return;
        SkinManager.Instance.OnSkinsChanged += Refresh;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && SkinManager.Instance != null) SkinManager.Instance.OnSkinsChanged -= Refresh;
        subscribed = false;
    }

    private void OnDestroy()
    {
        foreach (Sprite s in toSkin.Values) if (s != null) Destroy(s);
    }

    private void Refresh()
    {
        TrySubscribe();
        SkinDef def = SkinManager.Instance != null ? SkinManager.Instance.EquippedDef(target) : null;
        Texture2D next = def != null ? def.texture : null;
        if (next != null && baseTexture != null && (next.width != baseTexture.width || next.height != baseTexture.height))
        {
            Debug.LogWarning($"[Skins] {def.id} texture {next.width}x{next.height} doesn't match {target}'s base {baseTexture.width}x{baseTexture.height}; showing Classic.");
            next = null;
        }
        if (next == skinTexture) return;
        // Snap a currently-shown skin frame back to its base frame before switching textures.
        if (sr != null && sr.sprite != null && toBase.TryGetValue(sr.sprite, out Sprite b)) sr.sprite = b;
        foreach (Sprite s in toSkin.Values) if (s != null) Destroy(s);
        toSkin.Clear(); toBase.Clear();
        skinTexture = next;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (skinTexture == null || sr == null) return;
        Sprite s = sr.sprite;
        if (s == null || s.texture != baseTexture) return;
        if (!toSkin.TryGetValue(s, out Sprite skinned))
        {
            Rect r = s.rect;
            skinned = Sprite.Create(skinTexture, r, new Vector2(s.pivot.x / r.width, s.pivot.y / r.height), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            skinned.name = s.name + "_skin";
            toSkin[s] = skinned;
            toBase[skinned] = s;
        }
        sr.sprite = skinned;
    }
}
