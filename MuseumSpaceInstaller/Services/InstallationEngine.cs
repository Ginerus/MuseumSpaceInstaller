using MuseumSpaceInstaller.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MuseumSpaceInstaller.Services
{
    public class InstallationProgress
    {
        public string Stage { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public string Detail { get; set; } = string.Empty;
        public bool IsIndeterminate { get; set; }
    }

    public class InstallationEngine
    {
        private readonly Manifest _manifest;
        private readonly string _installPath;
        private readonly bool _createDesktopShortcut;
        private readonly Action<InstallationProgress>? _onProgress;

        public InstallationEngine(Manifest manifest, string installPath, bool createDesktopShortcut, Action<InstallationProgress>? onProgress = null)
        {
            _manifest = manifest;
            _installPath = installPath;
            _createDesktopShortcut = createDesktopShortcut;
            _onProgress = onProgress;
        }

        public async Task<bool> InstallAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Report("Проверка системы", 5, "Определение архитектуры ОС...");
                string arch = SystemChecker.GetOsArchitecture();
                await Task.Delay(300, cancellationToken);

                Report("Проверка .NET Runtime", 15, "Проверка установленного .NET Desktop Runtime 8...");
                bool hasRuntime = SystemChecker.IsDotNet8DesktopRuntimeInstalled();
                await Task.Delay(300, cancellationToken);

                if (!hasRuntime)
                {
                    Report("Установка .NET Runtime", 20, "Установка .NET Desktop Runtime 8...", true);
                    bool runtimeInstalled = await InstallDotNetRuntimeAsync(arch, cancellationToken);
                    if (!runtimeInstalled)
                    {
                        MessageBox.Show("Не удалось установить .NET Desktop Runtime 8. Установка прервана.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }

                Report("Подготовка установки", 30, "Создание каталога приложения...");
                Directory.CreateDirectory(_installPath);
                await Task.Delay(200, cancellationToken);

                // Сохранение конфиг-файлов при обновлении
                var backupDir = Path.Combine(Path.GetTempPath(), $"MuseumSpace_Backup_{Guid.NewGuid()}");
                bool isUpdate = Directory.Exists(_installPath) && File.Exists(Path.Combine(_installPath, _manifest.ExecutableName));
                if (isUpdate)
                {
                    Report("Подготовка установки", 32, "Сохранение пользовательских настроек...");
                    Directory.CreateDirectory(backupDir);
                    foreach (var configFile in _manifest.ConfigFiles)
                    {
                        string source = Path.Combine(_installPath, configFile);
                        if (File.Exists(source))
                        {
                            string dest = Path.Combine(backupDir, configFile);
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            File.Copy(source, dest, true);
                        }
                    }
                }

                Report("Копирование файлов", 40, "Копирование файлов приложения...");
                string sourcePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Payload");
                if (Directory.Exists(sourcePath))
                {
                    await CopyDirectoryAsync(sourcePath, _installPath, cancellationToken);
                }

                // Копирование нужной архитектуры libvlc
                Report("Копирование файлов", 60, $"Копирование библиотек для архитектуры {arch}...");
                var archInfo = arch == "x64" ? _manifest.Architecture.X64 : _manifest.Architecture.X86;
                string libvlcSource = Path.Combine(sourcePath, archInfo.LibVlcPath);
                string libvlcDest = Path.Combine(_installPath, "libvlc");
                if (Directory.Exists(libvlcSource))
                {
                    await CopyDirectoryAsync(libvlcSource, libvlcDest, cancellationToken);
                }

                // Удаление ненужных архитектур libvlc если они попали
                string libvlcX86 = Path.Combine(_installPath, "libvlc", "win-x86");
                string libvlcX64 = Path.Combine(_installPath, "libvlc", "win-x64");
                if (arch == "x64" && Directory.Exists(libvlcX86))
                    Directory.Delete(libvlcX86, true);
                if (arch == "x86" && Directory.Exists(libvlcX64))
                    Directory.Delete(libvlcX64, true);

                // Восстановление конфиг-файлов
                if (isUpdate && Directory.Exists(backupDir))
                {
                    Report("Копирование файлов", 65, "Восстановление пользовательских настроек...");
                    foreach (var configFile in _manifest.ConfigFiles)
                    {
                        string backupFile = Path.Combine(backupDir, configFile);
                        string destFile = Path.Combine(_installPath, configFile);
                        if (File.Exists(backupFile))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                            File.Copy(backupFile, destFile, true);
                        }
                    }
                    try { Directory.Delete(backupDir, true); } catch { }
                }

                Report("Создание ярлыков", 75, "Создание ярлыков...");
                string iconPath = Path.Combine(_installPath, "logo.ico");
                if (File.Exists(iconPath))
                {
                    if (_manifest.Shortcuts.StartMenu)
                        ShortcutHelper.CreateStartMenuShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName), iconPath);
                    if (_createDesktopShortcut && _manifest.Shortcuts.Desktop)
                        ShortcutHelper.CreateDesktopShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName), iconPath);
                }
                else
                {
                    if (_manifest.Shortcuts.StartMenu)
                        ShortcutHelper.CreateStartMenuShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName));
                    if (_createDesktopShortcut && _manifest.Shortcuts.Desktop)
                        ShortcutHelper.CreateDesktopShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName));
                }
                await Task.Delay(200, cancellationToken);

                Report("Регистрация приложения", 85, "Регистрация в системе...");
                RegistryHelper.RegisterApplication(_manifest, _installPath);
                await Task.Delay(200, cancellationToken);

                Report("Создание деинсталлятора", 90, "Создание программы удаления...");
                UninstallerHelper.CreateUninstaller(_installPath, _manifest);
                await Task.Delay(200, cancellationToken);

                Report("Завершение установки", 100, "Установка завершена успешно!");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка установки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task<bool> InstallDotNetRuntimeAsync(string arch, CancellationToken cancellationToken)
        {
            try
            {
                var archInfo = arch == "x64" ? _manifest.Architecture.X64 : _manifest.Architecture.X86;
                string installerName = Path.GetFileName(archInfo.DotnetRuntimeInstaller);
                string tempPath = Path.Combine(Path.GetTempPath(), installerName);

                // Извлечение встроенного установщика из ресурсов
                string resourceName = $"MuseumSpaceInstaller.Resources.{installerName}";
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fs, cancellationToken);
                }
                else
                {
                    // Fallback: поиск рядом с exe
                    string localPath = Path.Combine(Path.GetDirectoryName(assembly.Location)!, "dotnet-runtime", installerName);
                    if (File.Exists(localPath))
                    {
                        File.Copy(localPath, tempPath, true);
                    }
                    else
                    {
                        return false;
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/install /quiet /norestart",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;
                await process.WaitForExitAsync(cancellationToken);

                try { File.Delete(tempPath); } catch { }

                return process.ExitCode == 0 || process.ExitCode == 3010;
            }
            catch { return false; }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                await Task.Run(() => File.Copy(file, destFile, true), cancellationToken);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                await CopyDirectoryAsync(subDir, destSubDir, cancellationToken);
            }
        }

        private void Report(string stage, int percent, string detail, bool indeterminate = false)
        {
            _onProgress?.Invoke(new InstallationProgress
            {
                Stage = stage,
                ProgressPercent = percent,
                Detail = detail,
                IsIndeterminate = indeterminate
            });
        }
    }
}