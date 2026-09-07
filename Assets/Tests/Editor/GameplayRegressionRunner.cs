using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;
using NUnit.Framework.Internal.Execution;

namespace CardWars.Tests
{
    public static class GameplayRegressionRunner
    {
        private sealed class MainThreadDispatcher : IWorkItemDispatcher
        {
            private WorkItem root;
            public void Dispatch(WorkItem work)
            {
                if (root == null) root = work;
                work.Execute();
            }
            public void CancelRun(bool force) { if (root != null) root.Cancel(force); }
        }

        // Keep NUnit's discovery, work items, setup/teardown and assertions. The
        // bundled NUnit 3.5 default dispatcher creates a worker even with zero
        // workers configured, so dispatch synchronously for Unity's native APIs.
        public static ITestResult Run(string resultPath)
        {
            NUnitTestAssemblyRunner runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            Dictionary<string, object> settings = new Dictionary<string, object>();
            settings["NumberOfTestWorkers"] = 0;
            runner.Load(typeof(GameplayRegressionTests).Assembly, settings);
            CategoryFilter filter = new CategoryFilter("GameplayRegression");
            if (runner.CountTestCases(filter) == 0)
                throw new InvalidOperationException("No gameplay regression tests were discovered.");
            MainThreadDispatcher dispatcher = new MainThreadDispatcher();
            TestExecutionContext context = new TestExecutionContext
            {
                Dispatcher = dispatcher,
                WorkDirectory = Directory.GetCurrentDirectory(),
                IsSingleThreaded = true
            };
            typeof(TestExecutionContext).GetProperty("Listener", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(context, TestListener.NULL, null);
            WorkItem work = WorkItem.CreateWorkItem(runner.LoadedTest, filter);
            work.InitializeContext(context);
            ITestExecutionContext previousContext = TestExecutionContext.CurrentContext;
            try { dispatcher.Dispatch(work); }
            finally
            {
                typeof(TestExecutionContext).GetProperty("CurrentContext", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetSetMethod(true).Invoke(null, new object[] { previousContext });
            }
            ITestResult result = work.Result;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath)));
            File.WriteAllText(resultPath, result.ToXml(true).OuterXml);
            return result;
        }
    }
}
