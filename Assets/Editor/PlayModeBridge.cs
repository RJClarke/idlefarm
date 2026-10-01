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
        // "Type" calls Open() (or Show() if there's no Open); "Type.Method" calls that method;
        // "Type.Method|a|b" passes arguments (converted to the parameter types). Instance methods go
        // through the type's static Instance; static methods are called directly.
        string[] argText = new string[0];
        int bar = typeName.IndexOf('|');
        if (bar > 0) { argText = typeName.Substring(bar + 1).Split('|'); typeName = typeName.Substring(0, bar); }
        string methodName = null;
        int dot = typeName.IndexOf('.');
        if (dot > 0) { methodName = typeName.Substring(dot + 1); typeName = typeName.Substring(0, dot); }
        System.Type type = System.Type.GetType(typeName + ", Assembly-CSharp");
        if (type == null) return "TYPE NOT FOUND: " + typeName;

        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        // Matches the argument count; trailing optional parameters take their defaults.
        System.Reflection.MethodInfo Find(string n) => System.Linq.Enumerable.FirstOrDefault(type.GetMethods(flags), m =>
        {
            var ps = m.GetParameters();
            if (m.Name != n || ps.Length < argText.Length) return false;
            for (int i = argText.Length; i < ps.Length; i++) if (!ps[i].IsOptional && !ps[i].IsOut) return false;
            return true;
        });
        System.Reflection.MethodInfo method = methodName != null ? Find(methodName) : Find("Open") ?? Find("Show");
        if (method == null) return "NO " + (methodName ?? "Open/Show") + "(" + argText.Length + " args) ON: " + typeName;

        object instance = null;
        if (!method.IsStatic)
        {
            System.Reflection.PropertyInfo instanceProp = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            instance = instanceProp != null ? instanceProp.GetValue(null) : null;
            if (instance == null) return "INSTANCE NULL: " + typeName;
        }
        var ps2 = method.GetParameters();
        object[] args = new object[ps2.Length];
        for (int i = 0; i < ps2.Length; i++)
            args[i] = i < argText.Length
                ? (ps2[i].ParameterType.IsEnum ? System.Enum.Parse(ps2[i].ParameterType, argText[i])
                   : System.Convert.ChangeType(argText[i], ps2[i].ParameterType, System.Globalization.CultureInfo.InvariantCulture))
                : (ps2[i].IsOut ? null : ps2[i].DefaultValue);
        object ret = method.Invoke(instance, args);
        return "OK: " + typeName + "." + method.Name + "(" + string.Join(", ", argText) + ")" + (ret != null ? " -> " + ret : "");
    }
}
