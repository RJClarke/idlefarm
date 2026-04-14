using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Renames the already-sliced Cow_32x32 sheet to the chicken-style naming pattern.
// Expects 72 slices arranged top-to-bottom as: Idle, Eat, Walk — each section 6 frames x R/U/L/D.
public static class CowSheetRenamer
{
    private const string SHEET_PATH = "Assets/Sprites/Animals/Cows_32x32/Cow_32x32.png";
    private const string BASE_NAME = "Cow_32x32";
    // The sheet has 4 "preview" thumbnails in the top row before the real animation grid.
    private const int HEADER_PREVIEW_FRAMES = 4;

    // Sections in top-to-bottom order. Each section is (name, framesPerDirection).
    private static readonly (string name, int framesPerDir)[] SECTIONS = new (string, int)[]
    {
        ("Idle", 6),
        ("Eat",  6),
        ("Walk", 6),
    };

    private static readonly string[] DIRS = new[] { "R", "U", "L", "D" };

    [MenuItem("Tools/IdleFarm/Rename Cow Sheet")]
    public static void Rename()
    {
        TextureImporter ti = AssetImporter.GetAtPath(SHEET_PATH) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError($"CowSheetRenamer: TextureImporter not found at {SHEET_PATH}");
            return;
        }

        SpriteMetaData[] sprites = ti.spritesheet;
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError("CowSheetRenamer: sheet has no slices — slice it first in Sprite Editor");
            return;
        }

        int expectedMain = 0;
        foreach (var s in SECTIONS) expectedMain += s.framesPerDir * DIRS.Length;
        int expectedTotal = expectedMain + HEADER_PREVIEW_FRAMES;
        if (sprites.Length != expectedTotal)
        {
            Debug.LogError($"CowSheetRenamer: expected {expectedTotal} slices ({HEADER_PREVIEW_FRAMES} preview + {expectedMain} main), found {sprites.Length}.");
            return;
        }

        // Sort top-to-bottom, left-to-right. In texture coords, Y=0 is the BOTTOM,
        // so higher Y = higher up in the image. Group rows by Y bucket (cell-height tolerant).
        List<SpriteMetaData> sorted = new List<SpriteMetaData>(sprites);
        sorted.Sort((a, b) =>
        {
            // Bucket Y into rows by rounding to nearest cell-height. Use the smaller rect height
            // as a rough bucket size so tall/narrow variants in the same visual row still cluster.
            float yA = a.rect.yMax;
            float yB = b.rect.yMax;
            float tolerance = Mathf.Min(a.rect.height, b.rect.height) * 0.5f;
            if (Mathf.Abs(yA - yB) > tolerance) return yB.CompareTo(yA); // higher Y first
            return a.rect.xMin.CompareTo(b.rect.xMin);
        });

        // Header previews: one per direction (R, U, L, D).
        for (int p = 0; p < HEADER_PREVIEW_FRAMES && p < DIRS.Length; p++)
        {
            SpriteMetaData md = sorted[p];
            md.name = $"{BASE_NAME}_Preview{DIRS[p]}";
            sorted[p] = md;
        }

        int idx = HEADER_PREVIEW_FRAMES;
        foreach (var section in SECTIONS)
        {
            foreach (string dir in DIRS)
            {
                for (int f = 1; f <= section.framesPerDir; f++)
                {
                    SpriteMetaData md = sorted[idx];
                    md.name = $"{BASE_NAME}_{section.name}{dir}{f}";
                    sorted[idx] = md;
                    idx++;
                }
            }
        }

        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritesheet = sorted.ToArray();
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CowSheetRenamer: renamed {sorted.Count} slices on {SHEET_PATH}");
    }
}
