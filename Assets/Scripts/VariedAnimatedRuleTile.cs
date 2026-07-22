using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A RuleTile that delivers BOTH per-tile variety AND animation — something a stock RuleTile can't
/// (each of its rules is Random OR Animation, never both).
///
/// Each rule's <see cref="RuleTile.TilingRule.m_Sprites"/> is laid out VARIANT-MAJOR:
/// [v0f0, v0f1, v0f2, v0f3, v1f0, v1f1, …] — <see cref="m_FramesPerVariant"/> frames per variant.
/// For each cell we hash its position (same Perlin scheme the stock Random output uses) to pick ONE
/// variant deterministically, then hand that variant's frames to the tilemap animation system so it
/// loops. Neighbouring cells pick different variants, so the water is both varied and animated.
///
/// Built by Tools ▸ Lake Tiles ▸ Build Water Rule Tile.
/// </summary>
[CreateAssetMenu(fileName = "VariedAnimatedRuleTile", menuName = "2D/Tiles/Varied Animated Rule Tile")]
public class VariedAnimatedRuleTile : RuleTile
{
    [Tooltip("Animation frames each variant contributes to a rule's sprite list (sprites are stored " +
             "variant-major: all of variant 0's frames, then variant 1's, …).")]
    public int m_FramesPerVariant = 4;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        var iden = Matrix4x4.identity;
        tileData.sprite = m_DefaultSprite;
        tileData.gameObject = m_DefaultGameObject;
        tileData.colliderType = m_DefaultColliderType;
        tileData.flags = TileFlags.LockTransform;
        tileData.transform = iden;

        var transform = iden;
        foreach (var rule in m_TilingRules)
        {
            if (RuleMatches(rule, position, tilemap, ref transform))
            {
                int start = VariantStart(position, rule);
                if (rule.m_Sprites != null && rule.m_Sprites.Length > start)
                    tileData.sprite = rule.m_Sprites[start]; // frame 0 of the chosen variant
                tileData.transform = transform;
                tileData.gameObject = rule.m_GameObject;
                tileData.colliderType = rule.m_ColliderType;
                break;
            }
        }
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
        ref TileAnimationData tileAnimationData)
    {
        int fpv = Mathf.Max(1, m_FramesPerVariant);
        var transform = Matrix4x4.identity;
        foreach (var rule in m_TilingRules)
        {
            if (RuleMatches(rule, position, tilemap, ref transform))
            {
                if (rule.m_Sprites == null || rule.m_Sprites.Length < fpv) return false;
                int start = VariantStart(position, rule);
                var frames = new Sprite[fpv];
                Array.Copy(rule.m_Sprites, start, frames, 0, fpv);
                tileAnimationData.animatedSprites = frames;
                tileAnimationData.animationSpeed =
                    UnityEngine.Random.Range(rule.m_MinAnimationSpeed, rule.m_MaxAnimationSpeed);
                return true;
            }
        }
        return false;
    }

    /// <summary>Start index (frame 0) of the position-chosen variant within rule.m_Sprites.</summary>
    private int VariantStart(Vector3Int position, TilingRule rule)
    {
        int fpv = Mathf.Max(1, m_FramesPerVariant);
        int variantCount = Mathf.Max(1, rule.m_Sprites.Length / fpv);
        float perlin = GetPerlinValue(position, rule.m_PerlinScale, 100000f);
        int variant = Mathf.Clamp(Mathf.FloorToInt(perlin * variantCount), 0, variantCount - 1);
        return variant * fpv;
    }
}
