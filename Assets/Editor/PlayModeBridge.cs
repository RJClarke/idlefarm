using System.IO;
using UnityEditor;

/// <summary>
/// File-triggered play-mode control for headless/MCP workflows, mirroring EditModeTestBridge:
/// an external tool creates Temp/enter_play_mode.request or Temp/exit_play_mode.request and
/// this bridge toggles EditorApplication.isPlaying within ~2s. Temp/ is outside Assets so the
/// trigger files never churn the asset database. Editor-only; no runtime footprint.
/// </summary>
[InitializeOnLoad]
public static class PlayModeBridge
{
    private const string EnterRequestPath = "Temp/enter_play_mode.request";
    private const string ExitRequestPath = "Temp/exit_play_mode.request";
    private const string ScreenshotRequestPath = "Temp/screenshot.request";
    private const string ScreenshotOutputPath = "Temp/game_screenshot.png";
    private const string OpenPopupRequestPath = "Temp/open_popup.request";
    private const string OpenPopupResultPath = "Temp/open_popup_result.txt";

    private static double nextPoll;

    static PlayModeBridge()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;

        if (File.Exists(EnterRequestPath))
        {
            File.Delete(EnterRequestPath);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
        else if (File.Exists(ExitRequestPath))
        {
            File.Delete(ExitRequestPath);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
        else if (File.Exists(ScreenshotRequestPath))
        {
            // Captures the Game view INCLUDING overlay UI (uGUI + UITK) — unlike
            // look_at_game_view, which renders the world camera only.
            File.Delete(ScreenshotRequestPath);
            if (File.Exists(ScreenshotOutputPath)) File.Delete(ScreenshotOutputPath);
            UnityEngine.ScreenCapture.CaptureScreenshot(Path.GetFullPath(ScreenshotOutputPath));
        }
        else if (File.Exists(OpenPopupRequestPath))
        {
            // Request body is a type name, e.g. "BarnPopupUITK" — every *PopupUITK singleton
            // exposes `static X Instance` + `void Open()`, so this works for any of them without
            // per-popup plumbing. Play-mode only (Instance is null in edit mode).
            string typeName = File.ReadAllText(OpenPopupRequestPath).Trim();
            File.Delete(OpenPopupRequestPath);
            string result = OpenPopup(typeName);
            File.WriteAllText(OpenPopupResultPath, result);
        }
    }

    private static string OpenPopup(string typeName)
    {
        if (!EditorApplication.isPlaying) return "NOT PLAYING";
        System.Type type = System.Type.GetType(typeName + ", Assembly-CSharp");
        if (type == null) return "TYPE NOT FOUND: " + typeName;
        System.Reflection.PropertyInfo instanceProp = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        object instance = instanceProp != null ? instanceProp.GetValue(null) : null;
        if (instance == null) return "INSTANCE NULL: " + typeName;
        System.Reflection.MethodInfo openMethod = type.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (openMethod == null) return "NO Open() ON: " + typeName;
        openMethod.Invoke(instance, null);
        return "OK: " + typeName + ".Open()";
    }
}
