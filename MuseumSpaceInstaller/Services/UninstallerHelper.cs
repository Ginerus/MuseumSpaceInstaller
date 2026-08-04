using MuseumSpaceInstaller.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace MuseumSpaceInstaller.Services
{
    public static class UninstallerHelper
    {
        public static void CreateUninstaller(string installPath, Manifest manifest)
        {
            string uninstallerPath = Path.Combine(installPath, "Uninstall.exe");
            string assemblyPath = Process.GetCurrentProcess().MainModule!.FileName;
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
                    Directory.Delete(installPath, true);
                }

                MessageBox.Show("MuseumSpace успешно удалён.", "Удаление завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool IsUninstallMode()
        {
            string assemblyDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(assemblyDir)) return false;
            return File.Exists(Path.Combine(assemblyDir, ".uninstall"));
        }
    }
}