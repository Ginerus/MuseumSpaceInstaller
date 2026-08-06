using MuseumSpaceInstaller.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        private readonly bool _createStartMenuShortcut;
        private readonly Action<InstallationProgress>? _onProgress;
        private readonly Action<string>? _onLog;

        public InstallationEngine(Manifest manifest, string installPath, bool createDesktopShortcut, bool createStartMenuShortcut, Action<InstallationProgress>? onProgress = null, Action<string>? onLog = null)
        {
            _manifest = manifest;
            _installPath = installPath;
            _createDesktopShortcut = createDesktopShortcut;
            _createStartMenuShortcut = createStartMenuShortcut;
            _onProgress = onProgress;
            _onLog = onLog;
        }

        public async Task<bool> InstallAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Log("=== Начало установки MuseumSpace ===");
                Log($"Целевая папка: {_installPath}");

                Report("Проверка системы", 2, "Определение архитектуры ОС...", true);
                string arch = SystemChecker.GetOsArchitecture();
                Log($"Архитектура ОС: {arch}");
                await Task.Delay(200, cancellationToken);

                Report("Проверка .NET Runtime", 5, "Проверка установленного .NET Desktop Runtime 8...", true);
                bool hasRuntime = SystemChecker.IsDotNet8DesktopRuntimeInstalled();
                Log(hasRuntime ? ".NET 8 Desktop Runtime уже установлен" : ".NET 8 Desktop Runtime не найден");
                await Task.Delay(200, cancellationToken);

                if (!hasRuntime)
                {
                    Report("Установка .NET Runtime", 8, "Установка .NET Desktop Runtime 8...", true);
                    Log("Запуск установки .NET Desktop Runtime 8...");
                    bool runtimeInstalled = await InstallDotNetRuntimeAsync(arch, cancellationToken);
                    if (!runtimeInstalled)
                    {
                        MessageBox.Show("Не удалось установить .NET Desktop Runtime 8. Установка прервана.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    Log(".NET Desktop Runtime 8 установлен успешно");
                }

                Report("Подготовка установки", 12, "Создание каталога приложения...", true);
                Directory.CreateDirectory(_installPath);
                Log($"Создан каталог: {_installPath}");
                await Task.Delay(150, cancellationToken);

                // Сохранение конфиг-файлов при обновлении
                var backupDir = Path.Combine(Path.GetTempPath(), $"MuseumSpace_Backup_{Guid.NewGuid()}");
                bool isUpdate = Directory.Exists(_installPath) && File.Exists(Path.Combine(_installPath, _manifest.ExecutableName));
                if (isUpdate)
                {
                    Report("Подготовка установки", 14, "Сохранение пользовательских настроек...", true);
                    Log("Обнаружена предыдущая установка. Резервное копирование настроек...");
                    Directory.CreateDirectory(backupDir);
                    foreach (var configFile in _manifest.ConfigFiles)
                    {
                        string source = Path.Combine(_installPath, configFile);
                        if (File.Exists(source))
                        {
                            string dest = Path.Combine(backupDir, configFile);
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            File.Copy(source, dest, true);
                            Log($"  → backup: {configFile}");
                        }
                    }
                }

                // Извлечение Payload из ресурсов
                Report("Распаковка", 16, "Распаковка файлов установки...", true);
                Log("Извлечение Payload...");
                string sourcePath = ExtractPayload();
                if (string.IsNullOrEmpty(sourcePath))
                {
                    MessageBox.Show("Не удалось извлечь файлы для установки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                Log($"Payload распакован во временную папку");

                // Копирование файлов приложения (плавный прогресс 18% → 55%)
                Log("Начинаю копирование файлов приложения...");
                await CopyDirectoryWithProgressAsync(sourcePath, _installPath, 18, 55, cancellationToken);

                // Копирование нужной архитектуры libvlc (55% → 65%)
                var archInfo = arch == "x64" ? _manifest.Architecture.X64 : _manifest.Architecture.X86;
                string libvlcSource = Path.Combine(sourcePath, archInfo.LibVlcPath);
                string libvlcDest = Path.Combine(_installPath, "libvlc");
                if (Directory.Exists(libvlcSource))
                {
                    Log($"Копирование библиотек libvlc для архитектуры {arch}...");
                    await CopyDirectoryWithProgressAsync(libvlcSource, libvlcDest, 55, 65, cancellationToken);
                }

                // Удаление ненужных архитектур libvlc
                string libvlcX86 = Path.Combine(_installPath, "libvlc", "win-x86");
                string libvlcX64 = Path.Combine(_installPath, "libvlc", "win-x64");
                if (arch == "x64" && Directory.Exists(libvlcX86))
                {
                    Log("Удаление лишних библиотек x86...");
                    Directory.Delete(libvlcX86, true);
                }
                if (arch == "x86" && Directory.Exists(libvlcX64))
                {
                    Log("Удаление лишних библиотек x64...");
                    Directory.Delete(libvlcX64, true);
                }

                // Восстановление конфиг-файлов
                if (isUpdate && Directory.Exists(backupDir))
                {
                    Report("Восстановление настроек", 66, "Восстановление пользовательских настроек...", true);
                    Log("Восстановление пользовательских настроек...");
                    foreach (var configFile in _manifest.ConfigFiles)
                    {
                        string backupFile = Path.Combine(backupDir, configFile);
                        string destFile = Path.Combine(_installPath, configFile);
                        if (File.Exists(backupFile))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                            File.Copy(backupFile, destFile, true);
                            Log($"  → restore: {configFile}");
                        }
                    }
                    try { Directory.Delete(backupDir, true); } catch { }
                }

                // Ярлыки
                Report("Создание ярлыков", 78, "Создание ярлыков...", true);
                Log("Создание ярлыков...");
                string iconPath = Path.Combine(_installPath, "logo.ico");
                if (File.Exists(iconPath))
                {
                    if (_createStartMenuShortcut && _manifest.Shortcuts.StartMenu)
                    {
                        ShortcutHelper.CreateStartMenuShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName), iconPath);
                        Log("  → Ярлык в меню Пуск создан");
                    }
                    if (_createDesktopShortcut && _manifest.Shortcuts.Desktop)
                    {
                        ShortcutHelper.CreateDesktopShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName), iconPath);
                        Log("  → Ярлык на рабочем столе создан");
                    }
                }
                else
                {
                    if (_createStartMenuShortcut && _manifest.Shortcuts.StartMenu)
                    {
                        ShortcutHelper.CreateStartMenuShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName));
                        Log("  → Ярлык в меню Пуск создан");
                    }
                    if (_createDesktopShortcut && _manifest.Shortcuts.Desktop)
                    {
                        ShortcutHelper.CreateDesktopShortcut(_manifest.DisplayName, Path.Combine(_installPath, _manifest.ExecutableName));
                        Log("  → Ярлык на рабочем столе создан");
                    }
                }
                await Task.Delay(150, cancellationToken);

                // Реестр
                Report("Регистрация приложения", 88, "Регистрация в системе...", true);
                Log("Регистрация приложения в реестре...");
                RegistryHelper.RegisterApplication(_manifest, _installPath);
                await Task.Delay(150, cancellationToken);

                // Деинсталлятор
                Report("Создание деинсталлятора", 94, "Создание программы удаления...", true);
                Log("Создание Uninstall.exe...");
                UninstallerHelper.CreateUninstaller(_installPath, _manifest);
                await Task.Delay(150, cancellationToken);

                Report("Завершение установки", 100, "Установка завершена успешно!");
                Log("=== Установка завершена успешно ===");
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("!!! Установка отменена пользователем");
                throw;
            }
            catch (Exception ex)
            {
                Log($"!!! ОШИБКА: {ex.Message}");
                MessageBox.Show($"Ошибка установки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _onLog?.Invoke($"[{timestamp}] {message}");
        }

        private string ExtractPayload()
        {
            string appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            string devPayload = Path.Combine(appDir, "Payload");
            if (Directory.Exists(devPayload))
                return devPayload;

            string tempPayload = Path.Combine(Path.GetTempPath(), "MuseumSpace_Payload");
            if (Directory.Exists(tempPayload))
            {
                try { Directory.Delete(tempPayload, true); } catch { }
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("MuseumSpaceInstaller.Resources.Payload.zip");
                if (stream == null) return string.Empty;

                string zipPath = Path.Combine(Path.GetTempPath(), "MuseumSpace_Payload.zip");
                using (var fs = new FileStream(zipPath, FileMode.Create))
                {
                    stream.CopyTo(fs);
                }

                ZipFile.ExtractToDirectory(zipPath, tempPayload);
                try { File.Delete(zipPath); } catch { }

                string nestedPayload = Path.Combine(tempPayload, "Payload");
                if (Directory.Exists(nestedPayload))
                    return nestedPayload;

                return tempPayload;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task<bool> InstallDotNetRuntimeAsync(string arch, CancellationToken cancellationToken)
        {
            try
            {
                var archInfo = arch == "x64" ? _manifest.Architecture.X64 : _manifest.Architecture.X86;
                string installerName = Path.GetFileName(archInfo.DotnetRuntimeInstaller);
                string tempPath = Path.Combine(Path.GetTempPath(), installerName);

                string payloadPath = ExtractPayload();
                string payloadRuntime = Path.Combine(payloadPath, "dotnet-runtime", installerName);

                if (File.Exists(payloadRuntime))
                {
                    File.Copy(payloadRuntime, tempPath, true);
                }
                else
                {
                    string appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
                    string fallbackPath = Path.Combine(appDir, "Payload", "dotnet-runtime", installerName);
                    if (File.Exists(fallbackPath))
                    {
                        File.Copy(fallbackPath, tempPath, true);
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

        private void CollectFiles(string dir, List<string> files)
        {
            files.AddRange(Directory.GetFiles(dir));
            foreach (var subDir in Directory.GetDirectories(dir))
                CollectFiles(subDir, files);
        }

        private async Task CopyDirectoryWithProgressAsync(string sourceDir, string destDir, int progressStart, int progressEnd, CancellationToken cancellationToken)
        {
            var allFiles = new List<string>();
            CollectFiles(sourceDir, allFiles);
            int totalFiles = allFiles.Count;
            if (totalFiles == 0) return;

            Directory.CreateDirectory(destDir);
            int copied = 0;

            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(destDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

                await Task.Run(() => File.Copy(file, destFile, true), cancellationToken);

                copied++;
                int currentProgress = progressStart + (progressEnd - progressStart) * copied / totalFiles;
                string fileName = Path.GetFileName(file);

                Report("Копирование файлов", currentProgress, $"→ {relativePath}");
                Log($"  → {relativePath}");
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