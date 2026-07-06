using UnityEngine;

/// <summary>
/// Calls SaveManager.SaveGame() at the moments Unity guarantees we still have process time:
/// - OnApplicationPause(true): the OS backgrounded the app (Android home button, iOS swipe-up).
/// - OnApplicationQuit(): editor stop / desktop close / Android task killer.
///
/// This makes lastSeenUtcTicks accurate (welcome-back modal needs it) and ensures permanent
/// progression (UpgradeManager, HelperUpgradeManager) survives a real close, not just an
/// explicit "Save" tap. Attach to a scene-resident GameObject.
/// </summary>
[DefaultExecutionOrder(2000)]
public class AppLifecycleSaver : MonoBehaviour
{
    private void OnApplicationPause(bool paused)
    {
        if (paused) TrySave("OnApplicationPause");
    }

    private void OnApplicationQuit() => TrySave("OnApplicationQuit");

    private static void TrySave(string source)
    {
        if (SaveManager.Instance == null) return;
        try { SaveManager.Instance.SaveGame(); }
        catch (System.Exception e) { Debug.LogError($"[AppLifecycleSaver] {source} save failed: {e.Message}"); }
    }
}
