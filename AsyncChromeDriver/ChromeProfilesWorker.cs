// Copyright (c) Oleg Zudov. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Zu.WebBrowser.BasicTypes;

namespace Zu.Chrome
{
    public static class ChromeProfilesWorker
    {
        static ChromeProfilesWorker()
        {
            var platformName = GetPlatformString();
            if (platformName == "windows") {
                ChromeBinaryFileName = GetWindowsChromePath();
            } else if (platformName == "linux") {
                ChromeBinaryFileName = "/usr/bin/google-chrome";
            } else if (platformName == "mac") {
                ChromeBinaryFileName = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            }
        }


        private static string GetWindowsChromePath()
        {
            foreach (var rootFolder in new[] { "%ProgramFiles%", "%ProgramFiles(x86)%", "%LocalAppData%"})
            {
                var path = Path.Combine(rootFolder, @"Google\Chrome\Application\chrome.exe");
                if (File.Exists(path))
                    return path;
            }

#if !(NETSTANDARD2_0 || NETCOREAPP2_0)
            using (var regKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe") ??
                                Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
            {
                if (regKey != null)
                {
                    // Try to get the "(Default)" value
                    var path = regKey.GetValue(null) as string;
                    if (File.Exists(path))
                        return path;
                }
            }
#endif

            return null;
        }

        public static string ChromeBinaryFileName { get; set; }

        public static TimeSpan ChromeWarmupDelay { get; set; } = TimeSpan.FromSeconds(0.5);

        public static ChromeProcessInfo OpenChromeProfile(string userDir, int port = 5999, bool isHeadless = false, WebSize windowSize = null)
        {
            return OpenChromeProfile(new ChromeDriverConfig { UserDir = userDir, Port = port, Headless = isHeadless, WindowSize = windowSize });
        }

        public static ChromeProcessInfo OpenChromeProfile(ChromeDriverConfig config)
        {
            //if (string.IsNullOrWhiteSpace(userDir)) throw new ArgumentNullException(nameof(userDir));
            if (config.Port < 1 || config.Port > 65000) throw new ArgumentOutOfRangeException(nameof(config.Port));
            bool firstRun = false;
            if (!string.IsNullOrWhiteSpace(config.UserDir) && !Directory.Exists(config.UserDir)) {
                firstRun = true;
                Directory.CreateDirectory(config.UserDir);
            }

            var args = "--remote-debugging-port=" + config.Port
                // Chrome 111 rejects debugging web socket connections with a defined Origin header
                // See https://chromium-review.googlesource.com/c/chromium/src/+/4106102
                // There should be no Origin header in chrome session WebSocket (see ChromeSession.ChromeSession).
                // But by default WebSocket Origin is set to "", and there is no way to remove Origin (null doesn't work).
                // So it is required to use --remote-allow-origins=* to bypass this.
                // Or you can set some same origin here and in ChromeSession.ChromeSession (new WebSocket)
                + " --remote-allow-origins=*"
                + (string.IsNullOrWhiteSpace(config.UserDir) ? "" : " --user-data-dir=\"" + config.UserDir + "\"")
                + (firstRun ? " --bwsi --no-first-run" : "")
                + (config.Headless ? " --headless --disable-gpu" : "")
                + (config.WindowSize != null ? $" --window-size={config.WindowSize.Width},{config.WindowSize.Height}" : "")
                + (string.IsNullOrWhiteSpace(config.CommandLineArguments) ? "" : " " + config.CommandLineArguments);

            if (!File.Exists(ChromeBinaryFileName))
                throw new WebBrowserException($"Google Chrome was not found at {ChromeBinaryFileName}");

            if (config.Headless) {
                var process = new ProcessWithJobObject();
                process.StartProc(ChromeBinaryFileName, args);
                Thread.Sleep(ChromeWarmupDelay);
                return new ChromeProcessInfo { ProcWithJobObject = process, UserDir = config.UserDir, Port = config.Port };
            } else {
                var process = new Process();
                process.StartInfo.FileName = ChromeBinaryFileName;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                Thread.Sleep(ChromeWarmupDelay);
                return new ChromeProcessInfo { Proc = process, UserDir = config.UserDir, Port = config.Port };
            }
        }


        public static string GetPlatformString()
        {
            string platformName = "unknown";
#if NETSTANDARD2_0 || NETCOREAPP2_0
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                platformName = "windows";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                platformName = "linux";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                platformName = "mac";
            }
#else
            // Unfortunately, detecting the currently running platform isn't as
            // straightforward as you might hope.
            // See: http://mono.wikia.com/wiki/Detecting_the_execution_platform
            // and https://msdn.microsoft.com/en-us/library/3a8hyw88(v=vs.110).aspx
            const int PlatformMonoUnixValue = 128;
            PlatformID platformId = Environment.OSVersion.Platform;
            if (platformId == PlatformID.Unix || platformId == PlatformID.MacOSX || (int)platformId == PlatformMonoUnixValue)
            {
                using (Process unameProcess = new Process())
                {
                    unameProcess.StartInfo.FileName = "uname";
                    unameProcess.StartInfo.UseShellExecute = false;
                    unameProcess.StartInfo.RedirectStandardOutput = true;
                    unameProcess.Start();
                    unameProcess.WaitForExit(1000);
                    string output = unameProcess.StandardOutput.ReadToEnd();
                    if (output.ToLowerInvariant().StartsWith("darwin"))
                    {
                        platformName = "mac";
                    }
                    else
                    {
                        platformName = "linux";
                    }
                }
            }
            else if (platformId == PlatformID.Win32NT || platformId == PlatformID.Win32S || platformId == PlatformID.Win32Windows || platformId == PlatformID.WinCE)
            {
                platformName = "windows";
            }
#endif
            return platformName;
        }
    }
}
