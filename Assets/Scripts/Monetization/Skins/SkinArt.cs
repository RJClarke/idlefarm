using System.Collections.Generic;
using UnityEngine;

/// <summary>Preview sprites for Store cards: animals = the AnimalData icon frame's rect cut from the variant
/// texture; buildings = the skin sprite; Classic = the base art. Cached for the session.</summary>
public static class SkinArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => cache.Clear();

    public static Sprite ClassicPreview(string target)
    {
        if (target == "farmhouse") return BuildingSkinApplier.FarmhouseBase;
        AnimalData data = AnimalManager.Instance != null ? AnimalManager.Instance.GetAnimalData(target) : null;
        return data != null ? data.iconSprite : null;
    }

    public static Sprite Preview(SkinDef def)
    {
        if (def == null) return null;
        if (def.kind == SkinKind.BuildingSprite) return def.sprite;
        if (cache.TryGetValue(def.id, out Sprite s) && s != null) return s;
        Sprite icon = ClassicPreview(def.target);
        if (icon == null || def.texture == null || icon.texture.width != def.texture.width || icon.texture.height != def.texture.height) return icon;
        Rect r = icon.rect;
        s = Sprite.Create(def.texture, r, new Vector2(icon.pivot.x / r.width, icon.pivot.y / r.height), icon.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        cache[def.id] = s;
        return s;
    }
}
