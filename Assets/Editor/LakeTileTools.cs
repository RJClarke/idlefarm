using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Editor helpers for the lake water tiles cut from CL_MainLev.png.
///
/// The pond is a 6×6 block of 32px cells whose top-left cell sits at texture (768, 352). The ENTIRE
/// 6×6 block is repeated 3 more times directly to the right with no gaps, so those copies are the
/// 4 animation frames — any cell's frames live at x, x+192, x+384, x+576 at the SAME y (192 = 6
/// columns × 32). Unity sprite rects are bottom-left origin, so rows run DOWN from y=352 (row r →
/// y = 352 − r·32). Within one frame the 6×6 supplies VARIETY: ~16 interior fill variants and 4
/// variants per edge, so a lake doesn't look tiled.
///
/// A stock RuleTile rule is Random (variety) OR Animation, never both — so the builder emits a
/// custom <see cref="VariedAnimatedRuleTile"/> instead, which hashes each cell to a variant and
/// animates that variant's frames (variety AND motion). Sprites are stored variant-major
/// (v0f0,v0f1,v0f2,v0f3, v1f0,…).
///  • "Build Water Rule Tile (varied + animated)" — auto-shaping + variety + animation. (Recommended.)
///  • "Generate Water AnimatedTiles"              — one AnimatedTile per cell; animate by hand-placing.
///  • "Animate Selected Rule Tile"                — legacy: convert a plain Rule Tile to single-variant
///    animation (no variety). Rarely needed now the builder does both.
/// </summary>
public static class LakeTileTools
{
    private const string SheetPath = "Assets/Sprites/Environment/CL_MainLev.png";
    private const string OutputFolder = "Assets/Tiles/LakeWater";

    private const int PondLeftX = 768;   // texture x of the pond's left column (frame 0)
    private const int PondTopY  = 352;   // texture y (bottom-left origin) of the pond's TOP row
    private const int Cell      = 32;
    private const int Cols      = 6;
    private const int Rows      = 6;
    private const int Frames    = 4;
    private const int FrameStrideX = Cols * Cell; // 192 px between animation frames

    // Playback speed (frames/sec). A small range desyncs neighbouring tiles over time for a natural,
    // non-pulsing shimmer instead of the whole lake animating in lockstep.
    private const float MinSpeed = 3.5f;
    private const float MaxSpeed = 4.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Sheet sprite lookup
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>All sub-sprites of the sheet keyed by their rounded bottom-left texture (x,y).</summary>
    private static Dictionary<Vector2Int, Sprite> LoadSheetSprites()
    {
        var map = new Dictionary<Vector2Int, Sprite>();
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError($"[LakeTileTools] No sub-sprites at '{SheetPath}'. Is the texture sliced " +
                           "(Sprite Mode: Multiple) and the path correct?");
            return map;
        }
        foreach (Object o in assets)
        {
            if (o is Sprite s)
            {
                var key = new Vector2Int(Mathf.RoundToInt(s.rect.x), Mathf.RoundToInt(s.rect.y));
                map[key] = s;
            }
        }
        return map;
    }

    /// <summary>The single frame-0 sprite at texture (x,y), or null.</summary>
    private static Sprite FrameZero(Dictionary<Vector2Int, Sprite> map, int x, int y)
    {
        map.TryGetValue(new Vector2Int(x, y), out Sprite s);
        return s;
    }

    /// <summary>The 4 animation frames for a cell whose frame-0 rect is (x0, y). Null entries logged.</summary>
    private static Sprite[] FramesAt(Dictionary<Vector2Int, Sprite> map, int x0, int y)
    {
        var frames = new Sprite[Frames];
        for (int f = 0; f < Frames; f++)
        {
            var key = new Vector2Int(x0 + f * FrameStrideX, y);
            map.TryGetValue(key, out frames[f]);
            if (frames[f] == null)
                Debug.LogWarning($"[LakeTileTools] Missing frame {f} at texture ({key.x},{key.y}). " +
                                 "Check the pond origin / slice.");
        }
        return frames;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tool 1: AnimatedTile per cell
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Lake Tiles/Generate Water AnimatedTiles (6x6)")]
    private static void GenerateAnimatedTiles()
    {
        var map = LoadSheetSprites();
        if (map.Count == 0) return;
        EnsureOutputFolder();

        int made = 0;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                int x0 = PondLeftX + c * Cell;
                int y  = PondTopY - r * Cell;
                Sprite[] frames = FramesAt(map, x0, y);
                if (frames[0] == null) continue; // blank cell — skip

                var tile = ScriptableObject.CreateInstance<AnimatedTile>();
                tile.m_AnimatedSprites = frames;
                tile.m_MinSpeed = MinSpeed;
                tile.m_MaxSpeed = MaxSpeed;
                tile.m_TileColliderType = Tile.ColliderType.Sprite;

                AssetDatabase.CreateAsset(tile, $"{OutputFolder}/Water_r{r}_c{c}.asset");
                made++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LakeTileTools] Generated {made} AnimatedTile(s) into {OutputFolder}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tool 2: Convert an authored Rule Tile's rules to 4-frame animation
    // (Trades away Random variety — one variant per shape.)
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Lake Tiles/Animate Selected Rule Tile (4 frames)")]
    private static void AnimateSelectedRuleTile()
    {
        var rule = Selection.activeObject as RuleTile;
        if (rule == null)
        {
            EditorUtility.DisplayDialog("Animate Rule Tile",
                "Select the Rule Tile asset first (in the Project window), then run this again.", "OK");
            return;
        }

        var map = LoadSheetSprites();
        if (map.Count == 0) return;

        int converted = 0, skipped = 0;
        foreach (RuleTile.TilingRule tr in rule.m_TilingRules)
        {
            Sprite baseSprite = (tr.m_Sprites != null && tr.m_Sprites.Length > 0) ? tr.m_Sprites[0] : null;
            if (baseSprite == null) { skipped++; continue; }

            // Normalise the sprite's x back to frame 0, then gather all 4 frames.
            int x = Mathf.RoundToInt(baseSprite.rect.x);
            int y = Mathf.RoundToInt(baseSprite.rect.y);
            int within = ((x - PondLeftX) % FrameStrideX + FrameStrideX) % FrameStrideX;
            int x0 = PondLeftX + within;

            Sprite[] frames = FramesAt(map, x0, y);
            if (frames[0] == null) { skipped++; continue; }

            tr.m_Sprites = frames;
            tr.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation;
            tr.m_MinAnimationSpeed = MinSpeed;
            tr.m_MaxAnimationSpeed = MaxSpeed;
            converted++;
        }

        EditorUtility.SetDirty(rule);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LakeTileTools] Animated {converted} rule(s) on '{rule.name}' " +
                  $"({skipped} skipped). Note: this replaces Random variety with one variant per shape.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tool 3: Build the Water Rule Tile — 9-slice + inner corners, Random variety
    // ─────────────────────────────────────────────────────────────────────────

    // Outer (convex) corners — one piece each, at the four grid corners.
    private const int NWr = 0, NWc = 0;
    private const int NEr = 0, NEc = 5;
    private const int SWr = 5, SWc = 0;
    private const int SEr = 5, SEc = 5;

    // Inner (concave) corners — 2×2 extras above the pond, frame-0 top-left (768,416). Addressed by
    // raw texture (x,y). Negative = not wired (concave joins fall back to fill).
    private const int InNWx = 768, InNWy = 416; // land in up-left diagonal
    private const int InNEx = 800, InNEy = 416; // land in up-right diagonal
    private const int InSWx = 768, InSWy = 384; // land in down-left diagonal
    private const int InSEx = 800, InSEy = 384; // land in down-right diagonal

    [MenuItem("Tools/Lake Tiles/Build Water Rule Tile (varied + animated)")]
    private static void BuildWaterRuleTile()
    {
        var map = LoadSheetSprites();
        if (map.Count == 0) return;

        // The tile must be the custom VariedAnimatedRuleTile. If a different-typed asset already sits
        // at the path (e.g. an earlier plain RuleTile), replace it — you'll repaint once, since a
        // painted cell can't change its tile's C# type in place.
        string path = $"{OutputFolder}/WaterRuleTile.asset";
        EnsureOutputFolder();
        var rt = AssetDatabase.LoadAssetAtPath<VariedAnimatedRuleTile>(path);
        if (rt == null)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            rt = ScriptableObject.CreateInstance<VariedAnimatedRuleTile>();
            AssetDatabase.CreateAsset(rt, path);
        }

        rt.m_FramesPerVariant = Frames;
        rt.m_TilingRules.Clear();
        rt.m_DefaultColliderType = Tile.ColliderType.Sprite;
        rt.m_DefaultSprite = FrameZero(map, PondLeftX + 2 * Cell, PondTopY - 2 * Cell); // an interior fill

        const int T = RuleTile.TilingRuleOutput.Neighbor.This;     // must be water
        const int X = RuleTile.TilingRuleOutput.Neighbor.NotThis;  // must be grass/empty

        // Order matters — first matching rule wins: outer corners → edges → inner corners → fill.
        // Corners are single cells; edges pull their whole run and fill pulls the whole interior, so
        // both come out as Random variety.
        AddRule(rt, map, Cells(NWr, NWr, NWc, NWc), n: X, w: X, s: T, e: T);
        AddRule(rt, map, Cells(NEr, NEr, NEc, NEc), n: X, e: X, s: T, w: T);
        AddRule(rt, map, Cells(SWr, SWr, SWc, SWc), s: X, w: X, n: T, e: T);
        AddRule(rt, map, Cells(SEr, SEr, SEc, SEc), s: X, e: X, n: T, w: T);

        AddRule(rt, map, Cells(0, 0, 1, 4), n: X, s: T, e: T, w: T); // top edge variants
        AddRule(rt, map, Cells(5, 5, 1, 4), s: X, n: T, e: T, w: T); // bottom edge variants
        AddRule(rt, map, Cells(1, 4, 0, 0), w: X, n: T, s: T, e: T); // left edge variants
        AddRule(rt, map, Cells(1, 4, 5, 5), e: X, n: T, s: T, w: T); // right edge variants

        AddRule(rt, map, Tex(InNWx, InNWy), n: T, s: T, e: T, w: T, nw: X);
        AddRule(rt, map, Tex(InNEx, InNEy), n: T, s: T, e: T, w: T, ne: X);
        AddRule(rt, map, Tex(InSWx, InSWy), n: T, s: T, e: T, w: T, sw: X);
        AddRule(rt, map, Tex(InSEx, InSEy), n: T, s: T, e: T, w: T, se: X);

        AddRule(rt, map, Cells(1, 4, 1, 4), n: T, s: T, e: T, w: T); // interior fill variants (catch-all)

        rt.UpdateNeighborPositions();
        EditorUtility.SetDirty(rt);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LakeTileTools] Built '{rt.name}' with {rt.m_TilingRules.Count} rules " +
                  "(varied + animated). Paint it on Tilemap_water; enter Play mode to see it move.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Tiles")) AssetDatabase.CreateFolder("Assets", "Tiles");
        if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Tiles", "LakeWater");
    }

    /// <summary>Frame-0 texture coords for a rectangular block of pond cells (inclusive row/col range).</summary>
    private static List<Vector2Int> Cells(int rowStart, int rowEnd, int colStart, int colEnd)
    {
        var list = new List<Vector2Int>();
        for (int r = rowStart; r <= rowEnd; r++)
            for (int c = colStart; c <= colEnd; c++)
                list.Add(new Vector2Int(PondLeftX + c * Cell, PondTopY - r * Cell));
        return list;
    }

    /// <summary>Single frame-0 texture coord (empty list if unwired, i.e. negative).</summary>
    private static List<Vector2Int> Tex(int x, int y)
    {
        var list = new List<Vector2Int>();
        if (x >= 0 && y >= 0) list.Add(new Vector2Int(x, y));
        return list;
    }

    /// <summary>
    /// Adds one tiling rule whose sprite pool is EVERY frame of EVERY given variant cell, laid out
    /// variant-major (v0f0..v0f3, v1f0..) so VariedAnimatedRuleTile can pick a variant per position
    /// and animate its frames. A variant cell is skipped unless all its frames are present.
    /// Neighbor args: 0 = ignore, T = must be water, X = must not be water.
    /// </summary>
    private static void AddRule(RuleTile rt, Dictionary<Vector2Int, Sprite> map, List<Vector2Int> variantCells,
        int n = 0, int s = 0, int e = 0, int w = 0, int ne = 0, int nw = 0, int se = 0, int sw = 0)
    {
        if (variantCells == null || variantCells.Count == 0) return; // unwired

        var sprites = new List<Sprite>();
        foreach (Vector2Int cell in variantCells)
        {
            Sprite[] fr = FramesAt(map, cell.x, cell.y);
            bool complete = true;
            for (int f = 0; f < fr.Length; f++) if (fr[f] == null) complete = false;
            if (complete) sprites.AddRange(fr); // append this variant's frames, in order
        }
        if (sprites.Count == 0)
        {
            Debug.LogWarning("[LakeTileTools] A rule had no complete variant at its cells — skipped.");
            return;
        }

        var rule = new RuleTile.TilingRule
        {
            m_Sprites = sprites.ToArray(),
            m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation, // cosmetic; the custom tile drives playback
            m_MinAnimationSpeed = MinSpeed,
            m_MaxAnimationSpeed = MaxSpeed,
            m_ColliderType = Tile.ColliderType.Sprite,
            m_Neighbors = new List<int>(),
            m_NeighborPositions = new List<Vector3Int>()
        };

        void Add(int val, int dx, int dy)
        {
            if (val == 0) return;
            rule.m_Neighbors.Add(val);
            rule.m_NeighborPositions.Add(new Vector3Int(dx, dy, 0));
        }
        Add(n, 0, 1);  Add(s, 0, -1); Add(e, 1, 0);  Add(w, -1, 0);
        Add(ne, 1, 1); Add(nw, -1, 1); Add(se, 1, -1); Add(sw, -1, -1);

        rt.m_TilingRules.Add(rule);
    }
}
