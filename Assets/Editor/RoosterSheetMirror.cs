using System.IO;
using UnityEditor;
using UnityEngine;

// Mirrors the Rooster_Black_and_Brown sheet's slicing + naming to every other rooster sheet.
// Only targets files whose filename starts with "Rooster_" so chickens/chicks are left alone.
public static class RoosterSheetMirror
{
    private const string TEMPLATE_PATH = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32/Rooster_Black_and_Brown_32x32.png";
    private const string TEMPLATE_BASE = "Rooster_Black_and_Brown_32x32";
    private const string FOLDER = "Assets/Sprites/Animals/Chickens_and_Roosters_32x32";
    private const string TARGET_PREFIX = "Rooster_";

    [MenuItem("Tools/IdleFarm/Mirror Rooster Sheet Slicing")]
    public static void Mirror()
    {
        TextureImporter tiTemplate = AssetImporter.GetAtPath(TEMPLATE_PATH) as TextureImporter;
        if (tiTemplate == null)
        {
            Debug.LogError($"RoosterSheetMirror: template importer not found at {TEMPLATE_PATH}");
            return;
        }

        SpriteMetaData[] template = tiTemplate.spritesheet;
        if (template == null || template.Length == 0)
        {
            Debug.LogError("RoosterSheetMirror: template sheet has no sprites to mirror");
            return;
        }

        string[] files = Directory.GetFiles(FOLDER, "*.png");
        string templateFile = Path.GetFileName(TEMPLATE_PATH);
        int done = 0;

        foreach (string file in files)
        {
            string path = file.Replace('\\', '/');
            string filename = Path.GetFileName(path);
            if (filename == templateFile) continue;
            if (!filename.StartsWith(TARGET_PREFIX)) continue;

            string baseName = Path.GetFileNameWithoutExtension(path);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"RoosterSheetMirror: no TextureImporter for {path}"); continue; }

            SpriteMetaData[] outSprites = new SpriteMetaData[template.Length];
            for (int i = 0; i < template.Length; i++)
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
        Debug.Log($"RoosterSheetMirror: mirrored {template.Length} sprites to {done} rooster sheet(s).");
    }

    private static string ExtractSuffix(string name, string templateBase)
    {
        if (name.StartsWith(templateBase + "_")) return name.Substring(templateBase.Length + 1);
        if (name.StartsWith(templateBase)) return name.Substring(templateBase.Length);
        return name;
    }
}
