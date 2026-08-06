using Microsoft.Win32;
using MuseumSpaceInstaller.Models;
using System.IO;

namespace MuseumSpaceInstaller.Services
{
    public static class RegistryHelper
    {
        public static void RegisterApplication(Manifest manifest, string installPath)
        {
            UnregisterApplication(manifest.ProductCode);

            string uninstallKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{manifest.ProductCode}";
            using var key = Registry.LocalMachine.CreateSubKey(uninstallKey);
            if (key == null) return;

            key.SetValue("DisplayName", manifest.Registry.DisplayName);
            key.SetValue("Publisher", manifest.Registry.Publisher);
            key.SetValue("DisplayVersion", manifest.Registry.DisplayVersion);
            key.SetValue("InstallLocation", installPath);
            key.SetValue("UninstallString", manifest.Registry.UninstallString.Replace("{INSTALL_DIR}", installPath));

            // Размер в КБ (DWORD) — Windows показывает в списке приложений
            long sizeBytes = SystemChecker.GetDirectorySizeBytes(installPath);
            int sizeKb = (int)(sizeBytes / 1024);
            if (sizeKb > 0)
                key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);

            // Дата установки (YYYYMMDD)
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

            string iconPath = Path.Combine(installPath, "logo.ico");
            if (File.Exists(iconPath))
                key.SetValue("DisplayIcon", iconPath);
            else
                key.SetValue("DisplayIcon", manifest.Registry.DisplayIcon.Replace("{INSTALL_DIR}", installPath));

            key.SetValue("NoModify", manifest.Registry.NoModify);
            key.SetValue("NoRepair", manifest.Registry.NoRepair);

            if (!string.IsNullOrEmpty(manifest.Registry.HelpLink))
                key.SetValue("HelpLink", manifest.Registry.HelpLink);
            if (!string.IsNullOrEmpty(manifest.Registry.UrlInfoAbout))
                key.SetValue("URLInfoAbout", manifest.Registry.UrlInfoAbout);
        }

        public static void UnregisterApplication(string productCode)
        {
            string[] keys = new[]
            {
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}"
            };

            foreach (var keyPath in keys)
            {
                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree(keyPath, false);
                }
                catch { }
            }
        }
    }
}