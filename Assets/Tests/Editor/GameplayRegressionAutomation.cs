using System;
using System.IO;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CardWars.Tests
{
    [InitializeOnLoad]
    public static class GameplayRegressionAutomation
    {
        private static bool pending;
        private static bool running;
        private static readonly string ResultPath = Path.GetFullPath("Logs/GameplayRegression.xml");

        static GameplayRegressionAutomation()
        {
            // Every successful script compilation reloads this editor-only class.
            // Batch runs and Play Mode transitions use their own lifecycle.
            if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode)
                Queue();
        }

        public static void Queue()
        {
            if (pending || running || Application.isBatchMode) return;
            pending = true;
            EditorApplication.update += RunWhenIdle;
        }

        private static void RunWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= RunWhenIdle;
            pending = false;
            RunFromMenu();
        }

        [MenuItem("Tools/Card Wars/Run Gameplay Regression Tests")]
        public static void RunFromMenu()
        {
            if (running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.LogWarning("Run gameplay regression tests outside Play Mode, after compilation.");
                return;
            }
            try { RunChecked(); }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }

        public static void RunChecked()
        {
            running = true;
            try
            {
                ITestResult result = GameplayRegressionRunner.Run(ResultPath);
                if (result.FailCount != 0 || result.SkipCount != 0 || result.InconclusiveCount != 0 || result.PassCount == 0)
                    throw new InvalidOperationException("Gameplay regression: " + result.PassCount + " passed, " +
                        result.FailCount + " failed, " + result.SkipCount + " skipped, " +
                        result.InconclusiveCount + " inconclusive. See " + ResultPath +
                        " or run the GameplayRegression category in Window > General > Test Runner.");
                UnityEngine.Debug.Log("Gameplay regression: " + result.PassCount + " passed. Report: " + ResultPath);
            }
            finally { running = false; }
        }

        // -executeMethod CardWars.Tests.GameplayRegressionAutomation.RunBatch
        public static void RunBatch()
        {
            try
            {
                RunChecked();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }

    public class GameplayRegressionBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            try { GameplayRegressionAutomation.RunChecked(); }
            catch (Exception exception) { throw new BuildFailedException(exception.Message); }
        }
    }

    public class GameplayRegressionAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string[] paths in new[] { imported, deleted, moved, movedFrom })
                foreach (string path in paths)
                    if (path.StartsWith("Assets/", StringComparison.Ordinal))
                    {
                        GameplayRegressionAutomation.Queue();
                        return;
                    }
        }
    }
}
