using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Silently saves all dirty open scenes right before Unity would otherwise pop the "Do you want
/// to save the scene?" modal — script recompiles (domain reload) and entering Play Mode. That
/// modal blocks the whole editor UI thread, which kills MCP/automation bridges mid-task; this
/// removes the trigger instead of relying on every caller to remember to save first.
/// </summary>
[InitializeOnLoad]
public static class AutoSaveOnReload
{
    static AutoSaveOnReload()
    {
        AssemblyReloadEvents.beforeAssemblyReload += SaveDirtyScenes;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SaveDirtyScenes();
    }

    private static void SaveDirtyScenes()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
        }
    }
}
