using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace MuseumSpaceInstaller.Services
{
    public static class SystemChecker
    {
        public static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static string GetOsArchitecture()
        {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }

        public static bool IsDotNet8DesktopRuntimeInstalled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");
                if (key != null)
                {
                    foreach (var version in key.GetValueNames())
                    {
                        if (version.StartsWith("8."))
                            return true;
                    }
                }

                using var key32 = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.WindowsDesktop.App");
                if (key32 != null)
                {
                    foreach (var version in key32.GetValueNames())
                    {
                        if (version.StartsWith("8."))
                            return true;
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (output.Contains("Microsoft.WindowsDesktop.App 8."))
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static long GetFreeSpaceBytes(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path) ?? path;
                var drive = new DriveInfo(root);
                return drive.AvailableFreeSpace;
            }
            catch { return 0; }
        }

        public static bool IsInstalled(string productCode, string appName, out string? version, out string? installLocation)
        {
            version = null;
            installLocation = null;

            string[] registryPaths = new[]
            {
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}"
            };

            foreach (var regPath in registryPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(regPath);
                    if (key != null)
                    {
                        var name = key.GetValue("DisplayName") as string;
                        var loc = key.GetValue("InstallLocation") as string;
                        var ver = key.GetValue("DisplayVersion") as string;

                        if (!string.IsNullOrEmpty(name) && name == appName)
                        {
                            installLocation = loc;
                            version = ver;

                            if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                            {
                                string exePath = Path.Combine(loc, "MuseumSpace.exe");
                                if (File.Exists(exePath))
                                    return true;
                            }
                        }
                    }
                }
                catch { }
            }

            string[] fallbackPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var basePath in fallbackPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(basePath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            var name = subKey.GetValue("DisplayName") as string;
                            if (name == appName)
                            {
                                var loc = subKey.GetValue("InstallLocation") as string;
                                var ver = subKey.GetValue("DisplayVersion") as string;

                                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                                {
                                    string exePath = Path.Combine(loc, "MuseumSpace.exe");
                                    if (File.Exists(exePath))
                                    {
                                        installLocation = loc;
                                        version = ver;
                                        return true;
                                    }
                                }
                            }
                        }
                        catch { continue; }
                    }
                }
                catch { }
            }

            return false;
        }

        public static bool IsApplicationRunning(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        public static bool IsSystemDirectory(string path)
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant();
            var systemPaths = new[]
            {
                @"c:\",
                @"c:\windows",
                @"c:\program files",
                @"c:\program files (x86)",
                @"c:\users",
                @"c:\programdata"
            };
            return systemPaths.Contains(full);
        }
    }
}