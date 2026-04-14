using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Copies the slice layout + naming pattern from the Chicken_White template
// to every other chicken/rooster/chick sheet in the same folder.
// Each target sheet's names are substituted with the target's filename base,
// so "Chicken_White_32x32_WalkR3" becomes "Rooster_Golden_32x32_WalkR3".
public static class ChickenSheetMirror
{
    private const string TEMPLATE_PATH = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32/Chicken_White_32x32.png";
    private const string TEMPLATE_BASE = "Chicken_White_32x32";
    private const string FOLDER = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32";

    [MenuItem("Tools/IdleFarm/Mirror Chicken Sheet Slicing")]
    public static void Mirror()
    {
        TextureImporter tiTemplate = AssetImporter.GetAtPath(TEMPLATE_PATH) as TextureImporter;
        if (tiTemplate == null)
        {
            Debug.LogError($"ChickenSheetMirror: template importer not found at {TEMPLATE_PATH}");
            return;
        }

        SpriteMetaData[] templateSprites = tiTemplate.spritesheet;
        if (templateSprites == null || templateSprites.Length == 0)
        {
            Debug.LogError("ChickenSheetMirror: template sheet has no sprites to mirror");
            return;
        }

        List<SpriteMetaData> template = new List<SpriteMetaData>(templateSprites);
        string[] files = Directory.GetFiles(FOLDER, "*.png");
        string templateFile = Path.GetFileName(TEMPLATE_PATH);
        int done = 0;
        int skipped = 0;

        foreach (string file in files)
        {
            string path = file.Replace('\\', '/');
            string filename = Path.GetFileName(path);
            if (filename == templateFile) { skipped++; continue; }

            string baseName = Path.GetFileNameWithoutExtension(path);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"ChickenSheetMirror: no TextureImporter for {path}"); continue; }

            SpriteMetaData[] outSprites = new SpriteMetaData[template.Count];
            for (int i = 0; i < template.Count; i++)
            {
                SpriteMetaData src = template[i];
                string suffix = ExtractSuffix(src.name, TEMPLATE_BASE);
                outSprites[i] = new SpriteMetaData
                {
                    name = baseName + "_" + suffix,
                    rect = src.rect,
                    pivot = src.pivot,
                    alignment = src.alignment,
                    border = src.border,
                };
            }

            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritesheet = outSprites;
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
            done++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ChickenSheetMirror: mirrored {template.Count} sprites to {done} sheet(s); skipped template.");
    }

    private static string ExtractSuffix(string name, string templateBase)
    {
        // Handles both "Chicken_White_32x32_WalkR3" and the known typo "Chicken_White_32x32WalkR4".
        if (name.StartsWith(templateBase + "_")) return name.Substring(templateBase.Length + 1);
        if (name.StartsWith(templateBase)) return name.Substring(templateBase.Length);
        return name;
    }
}
