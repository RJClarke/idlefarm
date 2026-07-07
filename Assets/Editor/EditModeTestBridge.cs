using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// File-triggered EditMode test runner for headless/MCP workflows. An external tool (or a
/// human) creates Temp/run_editmode_tests.request; this bridge notices within ~2s, runs the
/// full EditMode suite via TestRunnerApi, and writes a summary (+ every non-passing test) to
/// Temp/editmode_test_results.txt. Temp/ is not under Assets, so neither file churns the
/// asset database. Editor-only; no runtime footprint.
/// </summary>
[InitializeOnLoad]
public static class EditModeTestBridge
{
    private const string RequestPath = "Temp/run_editmode_tests.request";
    private const string ResultPath = "Temp/editmode_test_results.txt";

    private static double nextPoll;
    private static bool running;

    static EditModeTestBridge()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (running || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;
        if (!File.Exists(RequestPath)) return;

        File.Delete(RequestPath);
        if (File.Exists(ResultPath)) File.Delete(ResultPath);
        running = true;

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new ResultWriter());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        Debug.Log("[EditModeTestBridge] EditMode test run started.");
    }

    private class ResultWriter : ICallbacks
    {
        private readonly System.Text.StringBuilder failures = new System.Text.StringBuilder();

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            string summary = $"RESULT: {result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n";
            File.WriteAllText(ResultPath, summary + failures);
            Debug.Log($"[EditModeTestBridge] {summary.TrimEnd()}");
            running = false;
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.Test.IsSuite && result.TestStatus != TestStatus.Passed)
                failures.AppendLine($"{result.TestStatus}: {result.FullName}\n{result.Message}\n{result.StackTrace}");
        }
    }
}
