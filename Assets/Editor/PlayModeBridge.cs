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
    }
}
