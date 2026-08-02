using System;
using System.IO;
using IWshRuntimeLibrary;

namespace MuseumSpaceInstaller.Services
{
    public static class ShortcutHelper
    {
        public static void CreateDesktopShortcut(string name, string targetPath)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktopPath, $"{name}.lnk"), targetPath);
        }

        public static void CreateStartMenuShortcut(string name, string targetPath)
        {
            string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string appFolder = Path.Combine(startMenuPath, "Programs", "MuseumSpace");
            Directory.CreateDirectory(appFolder);
            CreateShortcut(Path.Combine(appFolder, $"{name}.lnk"), targetPath);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath)
        {
            try
            {
                var shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                shortcut.IconLocation = targetPath + ",0";
                shortcut.Save();
            }
            catch { }
        }
    }
}