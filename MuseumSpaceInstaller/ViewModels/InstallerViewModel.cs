using MuseumSpaceInstaller.Models;
using MuseumSpaceInstaller.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace MuseumSpaceInstaller.ViewModels
{
    public class InstallerViewModel : INotifyPropertyChanged
    {
        private readonly Manifest _manifest;
        private string _currentStage = "Добро пожаловать";
        private int _progressPercent;
        private string _progressDetail = "";
        private bool _isIndeterminate;
        private bool _isInstalling;
        private string _installPath;
        private bool _createDesktopShortcut = true;
        private string _statusMessage = "";
        private string _versionComparisonResult = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public InstallerViewModel()
        {
            _manifest = LoadManifest();
            _installPath = _manifest.DefaultInstallPath;

            CheckSystem();

            StartInstallCommand = new RelayCommand(async () => await StartInstallAsync(), () => !IsInstalling);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CancelCommand = new RelayCommand(() => Application.Current.Shutdown());
            FinishCommand = new RelayCommand(() => Application.Current.Shutdown());
        }

        public Manifest Manifest => _manifest;

        public string CurrentStage
        {
            get => _currentStage;
            set { _currentStage = value; OnPropertyChanged(); }
        }

        public int ProgressPercent
        {
            get => _progressPercent;
            set { _progressPercent = value; OnPropertyChanged(); }
        }

        public string ProgressDetail
        {
            get => _progressDetail;
            set { _progressDetail = value; OnPropertyChanged(); }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set { _isIndeterminate = value; OnPropertyChanged(); }
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set { _isInstalling = value; OnPropertyChanged(); ((RelayCommand)StartInstallCommand).RaiseCanExecuteChanged(); }
        }

        public string InstallPath
        {
            get => _installPath;
            set { _installPath = value; OnPropertyChanged(); }
        }

        public bool CreateDesktopShortcut
        {
            get => _createDesktopShortcut;
            set { _createDesktopShortcut = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string VersionComparisonResult
        {
            get => _versionComparisonResult;
            set { _versionComparisonResult = value; OnPropertyChanged(); }
        }

        public ICommand StartInstallCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand FinishCommand { get; }

        private Manifest LoadManifest()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MuseumSpaceInstaller.Resources.manifest.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<Manifest>(json) ?? new Manifest();
            }
            return new Manifest();
        }

        private void CheckSystem()
        {
            // Проверка версии
            var installedVersion = SystemChecker.GetInstalledVersion(_manifest.ApplicationName);
            if (!string.IsNullOrEmpty(installedVersion))
            {
                var comparison = CompareVersions(installedVersion, _manifest.Version);
                if (comparison == 0)
                {
                    VersionComparisonResult = "У вас уже установлена актуальная версия программы.";
                }
                else if (comparison > 0)
                {
                    VersionComparisonResult = $"На компьютере установлена более новая версия ({installedVersion}).\nПродолжить установку более старой версии?";
                }
                else
                {
                    VersionComparisonResult = $"Будет выполнено обновление с версии {installedVersion} до {_manifest.Version}.";
                }
            }
            else
            {
                VersionComparisonResult = $"Будет установлена версия {_manifest.Version}.";
            }

            // Проверка запущенного приложения
            if (SystemChecker.IsApplicationRunning(Path.GetFileNameWithoutExtension(_manifest.ExecutableName)))
            {
                StatusMessage = "Обнаружено, что MuseumSpace сейчас работает. Закройте программу для продолжения.";
            }
        }

        private int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');
            int max = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < max; i++)
            {
                int p1 = i < parts1.Length && int.TryParse(parts1[i], out var x1) ? x1 : 0;
                int p2 = i < parts2.Length && int.TryParse(parts2[i], out var x2) ? x2 : 0;
                if (p1 != p2) return p1.CompareTo(p2);
            }
            return 0;
        }

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку для установки",
                FolderName = InstallPath
            };
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FolderName;
                if (SystemChecker.IsSystemDirectory(path))
                {
                    MessageBox.Show("Установка в системные каталоги запрещена. Выберите другую папку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                InstallPath = Path.Combine(path, "MuseumSpace");
            }
        }

        private async Task StartInstallAsync()
        {
            // Проверка запущенного приложения
            if (SystemChecker.IsApplicationRunning(Path.GetFileNameWithoutExtension(_manifest.ExecutableName)))
            {
                var retry = MessageBox.Show(
                    "Обнаружено, что MuseumSpace сейчас работает.\nЗакройте программу для продолжения.",
                    "Программа запущена",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (retry == MessageBoxResult.Cancel) return;
                if (SystemChecker.IsApplicationRunning(Path.GetFileNameWithoutExtension(_manifest.ExecutableName)))
                {
                    MessageBox.Show("Программа всё ещё запущена. Установка отменена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Проверка свободного места
            long freeSpace = SystemChecker.GetFreeSpaceBytes(InstallPath);
            long requiredBytes = (long)_manifest.RequiredSpaceMB * 1024 * 1024;
            if (freeSpace < requiredBytes)
            {
                MessageBox.Show($"Недостаточно свободного места на диске.\nТребуется: {_manifest.RequiredSpaceMB} МБ\nДоступно: {freeSpace / 1024 / 1024} МБ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка системной директории
            if (SystemChecker.IsSystemDirectory(InstallPath))
            {
                MessageBox.Show("Установка в системные каталоги запрещена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка версии — если совпадает, спросить
            var installedVersion = SystemChecker.GetInstalledVersion(_manifest.ApplicationName);
            if (!string.IsNullOrEmpty(installedVersion) && CompareVersions(installedVersion, _manifest.Version) == 0)
            {
                var res = MessageBox.Show("У вас уже установлена актуальная версия программы.\nПереустановить?", "Версия совпадает", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.No) return;
            }
            else if (!string.IsNullOrEmpty(installedVersion) && CompareVersions(installedVersion, _manifest.Version) > 0)
            {
                var res = MessageBox.Show("На компьютере установлена более новая версия программы.\nПродолжить установку более старой версии?", "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.No) return;
            }

            IsInstalling = true;
            CurrentStage = "Установка";

            var engine = new InstallationEngine(_manifest, InstallPath, CreateDesktopShortcut, progress =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentStage = progress.Stage;
                    ProgressPercent = progress.ProgressPercent;
                    ProgressDetail = progress.Detail;
                    IsIndeterminate = progress.IsIndeterminate;
                });
            });

            var cts = new CancellationTokenSource();
            bool success = await engine.InstallAsync(cts.Token);

            IsInstalling = false;

            if (success)
            {
                CurrentStage = "Готово";
                ProgressPercent = 100;
                ProgressDetail = "Установка завершена успешно!";
            }
            else
            {
                CurrentStage = "Ошибка";
                ProgressDetail = "Установка не завершена.";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}