using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using UnityEditor.Scripting.Compilers;
using UnityEditor.Utils;
using UnityEngine;

namespace UnityEditor.PackageManager.ValidationSuite
{
    // Copied and adapted from UnityEditor.
    internal class Program : IDisposable
    {
        private ProcessOutputStreamReader _stdout;
        private ProcessOutputStreamReader _stderr;
        private Stream _stdin;
        public Process _process;

        protected Program()
        {
            _process = new Process();
        }

        public Program(ProcessStartInfo si)
            : this()
        {
            _process.StartInfo = si;
        }

        public void Start()
        {
            Start(null);
        }

        public void Start(EventHandler exitCallback)
        {
            if (exitCallback != null)
            {
                _process.EnableRaisingEvents = true;
                _process.Exited += exitCallback;
            }

            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.UseShellExecute = false;

            _process.Start();
            _stdout = new ProcessOutputStreamReader(_process, _process.StandardOutput);
            _stderr = new ProcessOutputStreamReader(_process, _process.StandardError);
            _stdin = _process.StandardInput.BaseStream;
        }

        public string GetAllOutput()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("stdout:\n");
            foreach (var s in GetStandardOutput())
            {
                sb.Append(s);
                sb.Append("\n");
            }
            sb.Append("stderr:\n");
            foreach (var s in GetErrorOutput())
            {
                sb.Append(s);
                sb.Append("\n");
            }
            return sb.ToString();
        }

        public bool HasExited
        {
            get
            {
                if (_process == null)
                    throw new InvalidOperationException("You cannot call HasExited before calling Start");
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public int ExitCode
        {
            get { return _process.ExitCode; }
        }

        public int Id
        {
            get { return _process.Id; }
        }

        public void Dispose()
        {
            Kill();
            _process.Dispose();
            _stdin?.Dispose();
        }

        public void Kill()
        {
            if (!HasExited)
            {
                _process.Kill();
                _process.WaitForExit();
            }
        }

        public Stream GetStandardInput()
        {
            return _stdin;
        }

        public string[] GetStandardOutput()
        {
            return _stdout.GetOutput();
        }

        public string GetStandardOutputAsString()
        {
            var output = GetStandardOutput();
            return GetOutputAsString(output);
        }

        public string[] GetErrorOutput()
        {
            return _stderr.GetOutput();
        }

        public string GetErrorOutputAsString()
        {
            var output = GetErrorOutput();
            return GetOutputAsString(output);
        }

        private static string GetOutputAsString(string[] output)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in output)
            {
                sb.Append(t);
                sb.Append("\n");
            }
            return sb.ToString();
        }

        private int SleepTimeoutMilliseconds
        {
            get { return 10; }
        }

        public void WaitForExit()
        {
            // Case 1111601: Process.WaitForExit hangs on OSX platform
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                while (!_process.HasExited)
                {
                    // Don't consume 100% of CPU while waiting for process to exit
                    Thread.Sleep(SleepTimeoutMilliseconds);
                }
            }
            else
            {
                _process.WaitForExit();
            }
        }

        public bool WaitForExit(int milliseconds)
        {
            // Case 1111601: Process.WaitForExit hangs on OSX platform
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var start = DateTime.Now;
                while (!_process.HasExited && (DateTime.Now - start).TotalMilliseconds < milliseconds)
                {
                    // Don't consume 100% of CPU while waiting for process to exit
                    Thread.Sleep(SleepTimeoutMilliseconds);
                }
                return _process.HasExited;
            }
            else
            {
                return _process.WaitForExit(milliseconds);
            }
        }
    }

    internal class NetCoreProgram : Program
    {
        static readonly string DotNetRuntimePath = EditorApplication.applicationContentsPath + "/NetCoreRuntime";
        static readonly string DotNetMuxerPath = Path.Combine(DotNetRuntimePath, Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet");

        public NetCoreProgram(string executable, string arguments, Action<ProcessStartInfo> setupStartInfo)
        {
            _process.StartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = DotNetMuxerPath,
                WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                Arguments = $"\"{executable}\" {arguments}"
            };

            _process.StartInfo.EnvironmentVariables["DOTNET_ROOT"] = DotNetRuntimePath;
            _process.StartInfo.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            setupStartInfo?.Invoke(_process.StartInfo);
        }
    }
}
