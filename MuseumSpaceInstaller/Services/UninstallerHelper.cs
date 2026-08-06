using MuseumSpaceInstaller.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace MuseumSpaceInstaller.Services
{
    public static class UninstallerHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);

        private const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

        public static void CreateUninstaller(string installPath, Manifest manifest)
        {
            string uninstallerPath = Path.Combine(installPath, "Uninstall.exe");
            string assemblyPath = Environment.ProcessPath!;
            File.Copy(assemblyPath, uninstallerPath, true);

            string markerPath = Path.Combine(installPath, ".uninstall");
            File.WriteAllText(markerPath, manifest.ProductCode);
        }

        public static void RunUninstall(string installPath, Manifest manifest)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить MuseumSpace?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcut = Path.Combine(desktopPath, $"{manifest.DisplayName}.lnk");
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                string appFolder = Path.Combine(startMenuPath, "Programs", "MuseumSpace");
                if (Directory.Exists(appFolder))
                    Directory.Delete(appFolder, true);

                RegistryHelper.UnregisterApplication(manifest.ProductCode);

                if (Directory.Exists(installPath))
                {
                    foreach (var configFile in manifest.ConfigFiles)
                    {
                        string configPath = Path.Combine(installPath, configFile);
                        if (File.Exists(configPath))
                        {
                            try { File.Delete(configPath); } catch { }
                        }
                    }

                    foreach (var file in Directory.GetFiles(installPath))
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (fileName.Equals(".uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                        try { File.Delete(file); } catch { }
                    }

                    foreach (var dir in Directory.GetDirectories(installPath))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }

                    string uninstallerPath = Path.Combine(installPath, "Uninstall.exe");
                    string markerPath = Path.Combine(installPath, ".uninstall");

                    MoveFileEx(uninstallerPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                    MoveFileEx(markerPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                    MoveFileEx(installPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                }

                MessageBox.Show("MuseumSpace успешно удалён. Некоторые файлы будут удалены после перезагрузки.", "Удаление завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool IsUninstallMode()
        {
            string? assemblyDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(assemblyDir)) return false;
            return File.Exists(Path.Combine(assemblyDir, ".uninstall"));
        }
    }
}