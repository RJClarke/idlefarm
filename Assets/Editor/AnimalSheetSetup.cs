using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-click setup for the animal spritesheets that arrived unsliced: Pig, Horse, Goose.
/// Slices the sheet on a grid, names every frame, builds the Idle/Walk (+Eat) clips and an
/// AnimatorController on the same AnimState convention every other animal uses
/// (0..3 Idle R/U/L/D, 4..7 Walk R/U/L/D, 8..11 Eat R/U/L/D), creates the *Visual prefab,
/// and wires the prefab + icon into the matching Animal_*.asset.
///
/// This generalises CowAnimGenerator/CowSheetRenamer, which only handled the one cow sheet.
/// Two sheet layouts are in play:
///   Cow-style  (Pig)          — 24x4 cells; a header row of 4 previews, then one row per
///                               section with the directions laid out as column groups.
///   Row-per-dir (Horse/Goose) — 6x8 cells; four Walk rows then four Idle rows, one direction
///                               per row, and the Idle rows are short (4 frames, not 6).
///
/// Re-slicing REPLACES the sheet's old auto-slices, so the Animal_*.asset icon references are
/// re-pointed at a named frame here — otherwise the popup icons would break.
/// </summary>
public static class AnimalSheetSetup
{
    private const string ANIM_ROOT   = "Assets/Sprites/Animations";
    private const string PREFAB_DIR  = "Assets/Prefabs/Animals";
    private const float  FRAME_RATE  = 12f;
    private const float  STATE_SPEED = 0.5f;
    private const int    PIXELS_PER_UNIT = 21; // matches every other animal sheet — keeps pixel density consistent

    private static readonly string[] DIRS = { "R", "U", "L", "D" };

    /// <summary>A named block of frames in the sheet, e.g. "Walk" with 6 frames per direction.</summary>
    private struct Section
    {
        public string Name;
        public int FramesPerDir;
        public Section(string name, int frames) { Name = name; FramesPerDir = frames; }
    }

    private class SheetSpec
    {
        public string SheetPath;
        public string BaseName;        // sprite/clip prefix, e.g. "Pig"
        public int Cell;               // square cell size in px
        public int Cols;
        public int Rows;
        public bool RowPerDirection;   // false = cow-style column groups
        public int PreviewCount;       // cow-style only: preview thumbs in the header row
        public Section[] Sections;
        public string AnimalDataPath;  // may be null if the animal has no AnimalData asset yet
        public string IconFrame;       // frame name to use as the popup icon
        public float VisualScale;
        public System.Type ExtraComponent; // e.g. a FarmGoose defender brain alongside AnimalVisual
        public string EggSpritePath;       // PassiveTimer animals: the sprite their drop uses
        public float EggScale;             // 0 = leave AnimalVisual's default
        public bool TightCropIcon;         // emit a <Base>_Icon slice cropped to the art's actual pixels
    }

    // Horse and Goose come from the same pack: 6x8 grid, Walk rows (6 frames) then Idle rows
    // (4 frames), one direction per row in D/U/R/L order.
    private static readonly string[] ROW_DIR_ORDER = { "D", "U", "R", "L" };

    private static SheetSpec Pig() => new SheetSpec
    {
        SheetPath = "Assets/Sprites/Animals/Pigs_32x32/Pig_Pink_32x32.png",
        BaseName = "Pig",
        Cell = 64, Cols = 24, Rows = 4,
        RowPerDirection = false,
        PreviewCount = 4,
        // NOTE: the pig sheet is Idle/Walk/Eat, NOT the cow sheet's Idle/Eat/Walk — same pack,
        // different row order. Verified by eye: his row 2 strides, row 3 has his snout on the floor.
        Sections = new[] { new Section("Idle", 6), new Section("Walk", 6), new Section("Eat", 6) },
        AnimalDataPath = "Assets/Data/Animals/Animal_Pig.asset",
        IconFrame = "Pig_IdleL1", // every other animal's popup icon is the left-facing idle frame
        VisualScale = 1.0f,
    };

    private static SheetSpec Horse() => new SheetSpec
    {
        SheetPath = "Assets/Sprites/Animals/Horse/Horse_with_shadow.png", // shadowed variant grounds him better
        BaseName = "Horse",
        Cell = 64, Cols = 6, Rows = 8,
        RowPerDirection = true,
        Sections = new[] { new Section("Walk", 6), new Section("Idle", 4) },
        AnimalDataPath = "Assets/Data/Animals/Animal_Horse.asset",
        IconFrame = "Horse_IdleL1",
        // This pack draws smaller than the cow/pig pack (43px of art vs 90px), so scale up to
        // sit right next to them rather than reading as a pony.
        VisualScale = 1.8f,
        // His shadowed sheet has the same off-to-one-side shadow as the goose's, and his art fills
        // only ~43px of a 64px cell, so the icon needs the same trim to sit centred and read big.
        TightCropIcon = true,
    };

    private static SheetSpec Goose() => new SheetSpec
    {
        SheetPath = "Assets/Sprites/Animals/Goose/Goose_with_shadow.png",
        BaseName = "Goose",
        Cell = 32, Cols = 6, Rows = 8,
        RowPerDirection = true,
        Sections = new[] { new Section("Walk", 6), new Section("Idle", 4) },
        AnimalDataPath = "Assets/Data/Animals/Animal_Goose.asset",
        IconFrame = "Goose_IdleL3", // the most upright, alert pose of the four left idles
        VisualScale = 2.1f, // small-pack correction, plus she should read as a big bird next to the chicken
        ExtraComponent = typeof(FarmGoose), // she defends during runs as well as laying
        // Brown, so her drop reads as a different (better) egg than the chicken's white one.
        EggSpritePath = "Assets/Sprites/Environment/Eggs/Egg_32x32.png",
        EggScale = 1.15f, // a goose egg out-sizes the chicken's 0.8
        // She is drawn low and a touch right inside her 32px cell, so a full-cell icon reads
        // off-centre in the equip button. Crop the icon to her actual pixels instead.
        TightCropIcon = true,
    };

    [MenuItem("Tools/IdleFarm/Animals/Setup Pig")]   public static void SetupPig()   => Run(Pig());
    [MenuItem("Tools/IdleFarm/Animals/Setup Horse")] public static void SetupHorse() => Run(Horse());
    [MenuItem("Tools/IdleFarm/Animals/Setup Goose")] public static void SetupGoose() => Run(Goose());

    [MenuItem("Tools/IdleFarm/Animals/Setup Pig + Horse + Goose")]
    public static void SetupAll()
    {
        Run(Pig());
        Run(Horse());
        Run(Goose());
    }

    private static void Run(SheetSpec spec)
    {
        if (!Slice(spec)) return;

        Dictionary<string, Sprite> sprites = LoadSprites(spec.SheetPath);
        if (sprites.Count == 0)
        {
            Debug.LogError($"[AnimalSheetSetup] {spec.BaseName}: no sprites after slicing {spec.SheetPath}");
            return;
        }

        string outDir = $"{ANIM_ROOT}/{spec.BaseName}";
        EnsureFolder(outDir);

        // AnimState wants Idle at 0..3 and Walk at 4..7; Eat (if the sheet has it) rides at 8..11,
        // which is the same slot AnimalVisual.TriggerPeck drives.
        List<(string state, string clipPath)> states = new List<(string, string)>();
        foreach (string block in new[] { "Idle", "Walk", "Eat" })
        {
            Section? sec = FindSection(spec, block);
            if (sec == null) continue;
            foreach (string dir in DIRS)
            {
                string stateName = $"{spec.BaseName}_{block}{dir}";
                string clipPath = BuildClip(stateName, $"{spec.BaseName}_{block}{dir}", sec.Value.FramesPerDir, sprites, outDir);
                states.Add((stateName, clipPath));
            }
        }

        string controllerPath = $"{outDir}/{spec.BaseName}.controller";
        BuildController(controllerPath, states);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        sprites.TryGetValue($"{spec.BaseName}_IdleD1", out Sprite defaultSprite);
        string prefabPath = $"{PREFAB_DIR}/{spec.BaseName}Visual.prefab";
        BuildPrefab(spec, ctrl, defaultSprite, prefabPath);

        WireAnimalData(spec, prefabPath, sprites);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AnimalSheetSetup] {spec.BaseName}: {sprites.Count} frames sliced, {states.Count} clips, " +
                  $"controller + {prefabPath}" + (spec.AnimalDataPath != null ? " + AnimalData wired." : " (no AnimalData — not wired)."));
    }

    private static Section? FindSection(SheetSpec spec, string name)
    {
        foreach (Section s in spec.Sections)
            if (s.Name == name) return s;
        return null;
    }

    // ── Slicing ─────────────────────────────────────────────────────────────

    private static bool Slice(SheetSpec spec)
    {
        var ti = AssetImporter.GetAtPath(spec.SheetPath) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError($"[AnimalSheetSetup] no TextureImporter at {spec.SheetPath}");
            return false;
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.SheetPath);
        if (tex == null)
        {
            Debug.LogError($"[AnimalSheetSetup] could not load texture {spec.SheetPath}");
            return false;
        }

        int expectedW = spec.Cols * spec.Cell;
        int expectedH = spec.Rows * spec.Cell;
        if (tex.width != expectedW || tex.height != expectedH)
        {
            Debug.LogError($"[AnimalSheetSetup] {spec.BaseName}: sheet is {tex.width}x{tex.height} but the " +
                           $"{spec.Cols}x{spec.Rows} grid of {spec.Cell}px cells expects {expectedW}x{expectedH}. " +
                           "Fix the layout spec before slicing.");
            return false;
        }

        List<SpriteMetaData> slices = new List<SpriteMetaData>();
        Rect iconFrameRect = Rect.zero;
        foreach ((string name, int col, int row) in EnumerateCells(spec))
        {
            // Unity texture space has y=0 at the BOTTOM; our rows count from the top.
            var r = new Rect(col * spec.Cell, tex.height - (row + 1) * spec.Cell, spec.Cell, spec.Cell);
            if (name == spec.IconFrame) iconFrameRect = r;
            slices.Add(new SpriteMetaData
            {
                name = name,
                rect = r,
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
            });
        }

        // A full-cell icon inherits however the artist positioned the animal inside its cell —
        // usually feet-at-the-bottom, which reads as "sitting low" in a centred UI slot. Emit an
        // extra slice trimmed to the opaque pixels so the equip button can centre it properly.
        if (spec.TightCropIcon && iconFrameRect.width > 0f)
        {
            Rect trimmed = TrimToOpaque(spec.SheetPath, iconFrameRect);
            if (trimmed.width > 0f)
            {
                slices.Add(new SpriteMetaData
                {
                    name = spec.BaseName + "_Icon",
                    rect = trimmed,
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                });
            }
            else Debug.LogWarning($"[AnimalSheetSetup] {spec.BaseName}: could not trim icon frame '{spec.IconFrame}'.");
        }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = PIXELS_PER_UNIT;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.spritesheet = slices.ToArray();

        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        return true;
    }

    /// <summary>Every (frameName, col, row) the sheet actually contains, in reading order.</summary>
    private static IEnumerable<(string name, int col, int row)> EnumerateCells(SheetSpec spec)
    {
        if (spec.RowPerDirection)
        {
            // Four rows per section, one direction each, in D/U/R/L order.
            int row = 0;
            foreach (Section sec in spec.Sections)
            {
                foreach (string dir in ROW_DIR_ORDER)
                {
                    for (int f = 0; f < sec.FramesPerDir; f++)
                        yield return ($"{spec.BaseName}_{sec.Name}{dir}{f + 1}", f, row);
                    row++;
                }
            }
            yield break;
        }

        // Cow-style: a header row of preview thumbs, then one row per section with the four
        // directions sitting side by side as column groups.
        for (int p = 0; p < spec.PreviewCount && p < DIRS.Length; p++)
            yield return ($"{spec.BaseName}_Preview{DIRS[p]}", p, 0);

        int sectionRow = 1;
        foreach (Section sec in spec.Sections)
        {
            for (int d = 0; d < DIRS.Length; d++)
                for (int f = 0; f < sec.FramesPerDir; f++)
                    yield return ($"{spec.BaseName}_{sec.Name}{DIRS[d]}{f + 1}", d * sec.FramesPerDir + f, sectionRow);
            sectionRow++;
        }
    }

    /// <summary>
    /// Shrink a cell rect to the bounding box of its non-transparent pixels. Decodes the PNG off
    /// disk into a throwaway texture, so the imported asset does not need Read/Write enabled.
    /// </summary>
    private static Rect TrimToOpaque(string sheetPath, Rect cell)
    {
        // Deliberately high: the shadowed sheets bake a soft drop shadow at ~96 alpha that hangs to
        // one side, so trimming on "any visible pixel" centres the icon on animal-plus-shadow and
        // leaves the animal itself sitting off to one side. 128 bounds the solid body only, which
        // also matches the unshadowed sheets' icons.
        const byte AlphaThreshold = 128;

        byte[] bytes;
        try { bytes = File.ReadAllBytes(sheetPath); }
        catch (System.Exception e) { Debug.LogWarning($"[AnimalSheetSetup] cannot read {sheetPath}: {e.Message}"); return Rect.zero; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) { Object.DestroyImmediate(tex); return Rect.zero; }

        int x0 = (int)cell.x, y0 = (int)cell.y, w = (int)cell.width, h = (int)cell.height;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        Color32[] px = tex.GetPixels32();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[(y0 + y) * tex.width + (x0 + x)].a < AlphaThreshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        Object.DestroyImmediate(tex);

        if (minX > maxX || minY > maxY) return Rect.zero; // fully transparent cell
        return new Rect(x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static Sprite FirstSpriteAt(string path)
    {
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
            if (obj is Sprite spr) return spr;
        return null;
    }

    private static Dictionary<string, Sprite> LoadSprites(string sheetPath)
    {
        var map = new Dictionary<string, Sprite>();
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (obj is Sprite spr) map[spr.name] = spr;
        return map;
    }

    // ── Clips + controller ──────────────────────────────────────────────────

    private static string BuildClip(string stateName, string framePrefix, int frameCount,
                                    Dictionary<string, Sprite> sprites, string outDir)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int f = 1; f <= frameCount; f++)
        {
            if (sprites.TryGetValue(framePrefix + f, out Sprite sp)) frames.Add(sp);
            else Debug.LogWarning($"[AnimalSheetSetup] missing frame '{framePrefix}{f}'");
        }

        var clip = new AnimationClip { frameRate = FRAME_RATE };
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[frames.Count];
        for (int f = 0; f < frames.Count; f++)
            keys[f] = new ObjectReferenceKeyframe { time = f / FRAME_RATE, value = frames[f] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        string clipPath = $"{outDir}/{stateName}.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clipPath;
    }

    private static void BuildController(string controllerPath, List<(string state, string clipPath)> states)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        ctrl.AddParameter("AnimState", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        var built = new List<AnimatorState>();

        foreach ((string stateName, string clipPath) in states)
        {
            AnimatorState state = sm.AddState(stateName);
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            state.writeDefaultValues = false;
            state.speed = STATE_SPEED;
            built.Add(state);
        }

        if (built.Count > 3) sm.defaultState = built[3]; // IdleD

        // States were added in AnimState order, so the index IS the AnimState value.
        for (int i = 0; i < built.Count; i++)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(built[i]);
            t.AddCondition(AnimatorConditionMode.Equals, i, "AnimState");
            t.duration = 0f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
        }
    }

    // ── Prefab + data wiring ────────────────────────────────────────────────

    private static void BuildPrefab(SheetSpec spec, AnimatorController ctrl, Sprite defaultSprite, string prefabPath)
    {
        if (ctrl == null)
        {
            Debug.LogError($"[AnimalSheetSetup] {spec.BaseName}: cannot build prefab without a controller.");
            return;
        }
        EnsureFolder(PREFAB_DIR);

        var go = new GameObject($"{spec.BaseName}Visual");
        go.transform.localScale = new Vector3(spec.VisualScale, spec.VisualScale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        if (defaultSprite != null) sr.sprite = defaultSprite;

        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        AnimalVisual visual = go.AddComponent<AnimalVisual>();
        if (spec.ExtraComponent != null) go.AddComponent(spec.ExtraComponent);

        if (!string.IsNullOrEmpty(spec.EggSpritePath) || spec.EggScale > 0f)
        {
            var vso = new SerializedObject(visual);
            if (!string.IsNullOrEmpty(spec.EggSpritePath))
            {
                Sprite egg = FirstSpriteAt(spec.EggSpritePath);
                if (egg != null) vso.FindProperty("eggSprite").objectReferenceValue = egg;
                else Debug.LogWarning($"[AnimalSheetSetup] {spec.BaseName}: no sprite at {spec.EggSpritePath}");
            }
            if (spec.EggScale > 0f) vso.FindProperty("eggScale").floatValue = spec.EggScale;
            vso.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
    }

    private static void WireAnimalData(SheetSpec spec, string prefabPath, Dictionary<string, Sprite> sprites)
    {
        if (string.IsNullOrEmpty(spec.AnimalDataPath)) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var data = AssetDatabase.LoadAssetAtPath<Object>(spec.AnimalDataPath);
        if (prefab == null || data == null)
        {
            Debug.LogError($"[AnimalSheetSetup] {spec.BaseName}: prefab or {spec.AnimalDataPath} missing — not wired.");
            return;
        }

        var so = new SerializedObject(data);
        so.FindProperty("visualPrefab").objectReferenceValue = prefab;

        // The old icon pointed at an auto-slice that slicing just destroyed — re-point it.
        // Prefer the trimmed icon slice so the art centres in the equip button.
        string iconKey = spec.TightCropIcon && sprites.ContainsKey(spec.BaseName + "_Icon")
            ? spec.BaseName + "_Icon"
            : spec.IconFrame;
        if (sprites.TryGetValue(iconKey, out Sprite icon))
            so.FindProperty("iconSprite").objectReferenceValue = icon;
        else
            Debug.LogWarning($"[AnimalSheetSetup] {spec.BaseName}: icon frame '{iconKey}' not found; icon left as-is.");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void EnsureFolder(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
    }
}
