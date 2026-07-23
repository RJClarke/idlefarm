using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// File-triggered object-reference wiring for headless/MCP workflows, mirroring PlayModeBridge:
/// an external tool writes Temp/wire_refs.request with one assignment per line —
/// "GameObjectPath|ComponentTypeName|FieldName|AssetPath" — and this bridge resolves each via
/// SerializedObject and saves the active scene. Temp/ is outside Assets so the trigger file never
/// churns the asset database. Editor-only; no runtime footprint.
/// </summary>
[InitializeOnLoad]
public static class AssetRefBridge
{
    private const string RequestPath = "Temp/wire_refs.request";
    private const string ResultPath = "Temp/wire_refs_result.txt";

    private static double nextPoll;

    static AssetRefBridge()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;

        if (!File.Exists(RequestPath)) return;
        string[] lines = File.ReadAllLines(RequestPath);
        File.Delete(RequestPath);

        var results = new System.Text.StringBuilder();
        bool anyApplied = false;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split('|');
            if (parts.Length != 4) { results.AppendLine($"MALFORMED: {line}"); continue; }

            string goPath = parts[0], typeName = parts[1], fieldName = parts[2], assetPath = parts[3];
            GameObject go = GameObject.Find(goPath);
            if (go == null) { results.AppendLine($"NOT FOUND (GameObject): {goPath}"); continue; }

            System.Type type = System.Type.GetType(typeName + ", Assembly-CSharp");
            Component comp = type != null ? go.GetComponent(type) : null;
            if (comp == null) { results.AppendLine($"NOT FOUND (Component {typeName} on {goPath})"); continue; }

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) { results.AppendLine($"NOT FOUND (Asset): {assetPath}"); continue; }

            var so = new SerializedObject(comp);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) { results.AppendLine($"NOT FOUND (Field {fieldName} on {typeName})"); continue; }

            prop.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            results.AppendLine($"OK: {goPath}.{fieldName} = {assetPath}");
            anyApplied = true;
        }

        if (anyApplied)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        File.WriteAllText(ResultPath, results.ToString());
    }
}
