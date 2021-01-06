using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityEditor.PackageManager.ValidationSuite
{
    public class TextReport
    {
        public string FilePath { get; set; }

        private const string exceptionSectionPlaceholder = "\r\nExceptions section\r\n";

        public TextReport(string packageId)
        {
            FilePath = ReportPath(packageId);
        }

        internal void Initialize(VettingContext context)
        {
            var packageInfo = context.ProjectPackageInfo;
            Write(
                string.Format("Validation Suite Results for package \"{0}\"\r\n", packageInfo.name) +
                string.Format(" - Path: {0}\r\n", packageInfo.path) +
                string.Format(" - Version: {0}\r\n", packageInfo.version) +
                string.Format(" - Type: {0}\r\n", context.PackageType) +
                string.Format(" - Context: {0}\r\n", context.ValidationType) +
                string.Format(" - Lifecycle: {0}\r\n", packageInfo.lifecycle) +
                string.Format(" - Test Time: {0}\r\n", DateTime.Now) +
                string.Format(" - Tested with {0} version: {1}\r\n", context.VSuiteInfo.name, context.VSuiteInfo.version)
            );

            if (context.ProjectPackageInfo.dependencies.Any())
            {
                Append("\r\nPACKAGE DEPENDENCIES:\r\n");
                Append("--------------------\r\n");
                foreach (var dependencies in context.ProjectPackageInfo.dependencies)
                {
                    Append(string.Format("    - {0}@{1}\r\n", dependencies.Key, dependencies.Value));
                }
            }

            Append(exceptionSectionPlaceholder);

            Append("\r\nVALIDATION RESULTS:\r\n");
            Append("------------------\r\n");
        }

        public void Clear()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }

        public void Write(string text)
        {
            File.WriteAllText(FilePath, text);
        }

        public void Append(string text)
        {
            File.AppendAllText(FilePath, text);
        }

        public void Replace(string text, string replacement)
        {
            string str = File.ReadAllText(FilePath);
            str = str.Replace(text, replacement);
            File.WriteAllText(FilePath, str);
        }

        public void GenerateReport(ValidationSuite suite)
        {
            SaveTestResult(suite, TestState.Failed);
            SaveTestResult(suite, TestState.Succeeded);
            SaveTestResult(suite, TestState.NotRun);
            SaveTestResult(suite, TestState.NotImplementedYet);
            PrintExceptions(suite.context); // make sure to print this at the end, when all exceptions were verified
        }

        void PrintExceptions(VettingContext context)
        {
            string str = "";
            if (context.ValidationExceptionManager.HasExceptions)
            {
                str = "\r\n\r\n***************************************\r\n";
                str += "PACKAGE CONTAINS VALIDATION EXCEPTIONS!\r\n";
                var issuesList = context.ValidationExceptionManager.CheckValidationExceptions(context.PublishPackageInfo.version);
                foreach (var issue in issuesList)
                {
                    str += "\r\n- Issue: " + issue + "\r\n";
                }
                str += "***************************************\r\n";
            }

            Replace(exceptionSectionPlaceholder, str);
        }

        void SaveTestResult(ValidationSuite suite, TestState testState)
        {
            if (suite.ValidationTests == null)
            {
                return;
            }

            foreach (var testResult in suite.ValidationTests.Where(t => t.TestState == testState))
            {
                Append(string.Format("\r\n{0} - \"{1}\"\r\n    ", testResult.TestState, testResult.TestName));
                if (testResult.TestOutput.Any())
                    Append(string.Join("\r\n\n    ", testResult.TestOutput.Select(o => o.ToString()).ToArray()) +
                        "\r\n    ");
            }
        }

        public static string ReportPath(string packageId)
        {
            return Path.Combine(ValidationSuiteReport.ResultsPath, packageId + ".txt");
        }

        public static bool ReportExists(string packageId)
        {
            return File.Exists(ReportPath(packageId));
        }
    }
}
