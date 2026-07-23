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
    private static double runStartedAt;
    // A run that errors before RunFinished fires (e.g. triggered while still in play mode, where
    // the test tree is unavailable) leaves `running` stuck true with no further callback ever
    // resetting it — and unlike a script edit, entering/exiting play mode alone never recompiles
    // to clear statics either. Treat a run older than this as abandoned rather than in-progress.
    private const double StaleRunSeconds = 30.0;

    static EditModeTestBridge()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (running && EditorApplication.timeSinceStartup - runStartedAt > StaleRunSeconds)
        {
            Debug.LogWarning("[EditModeTestBridge] Previous run never completed (stale) — resetting.");
            running = false;
        }
        if (running || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2.0;
        if (!File.Exists(RequestPath)) return;

        File.Delete(RequestPath);
        if (File.Exists(ResultPath)) File.Delete(ResultPath);
        running = true;
        runStartedAt = EditorApplication.timeSinceStartup;

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
