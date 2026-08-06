using Microsoft.Win32;
using MuseumSpaceInstaller.Models;
using MuseumSpaceInstaller.Services;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MuseumSpaceInstaller.ViewModels
{
    public class InstallerViewModel : INotifyPropertyChanged
    {
        private readonly Manifest _manifest;
        private string _currentStage = "Welcome";
        private int _progressPercent;
        private string _progressDetail = "";
        private bool _isIndeterminate;
        private bool _isInstalling;
        private string _installPath;
        private bool _createDesktopShortcut = true;
        private bool _createStartMenuShortcut = true;
        private string _statusMessage = "";
        private string _versionComparisonResult = "";
        private string? _existingInstallPath;
        private string? _existingVersion;
        private bool _isUpdate;
        private bool _isEulaAccepted;
        private string _eulaText = "Загрузка лицензионного соглашения...";
        public ObservableCollection<string> InstallationLog { get; } = new ObservableCollection<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public InstallerViewModel()
        {
            _manifest = LoadManifest();
            _installPath = _manifest.DefaultInstallPath;
            LoadEula();
            CheckSystem();

            NextCommand = new RelayCommand(GoNext, () => !IsInstalling && CanGoNext);
            BackCommand = new RelayCommand(GoBack, () => !IsInstalling && CanGoBack);
            StartInstallCommand = new RelayCommand(async () => await StartInstallAsync(), () => !IsInstalling);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CancelCommand = new RelayCommand(() => Application.Current.Shutdown());
            FinishCommand = new RelayCommand(() => Application.Current.Shutdown());
            LaunchAndFinishCommand = new RelayCommand(LaunchAndFinish);
        }

        public Manifest Manifest => _manifest;

        public string CurrentStage
        {
            get => _currentStage;
            set
            {
                _currentStage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
            }
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
            set
            {
                _isInstalling = value;
                OnPropertyChanged();
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
                ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StartInstallCommand).RaiseCanExecuteChanged();
            }
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

        public bool CreateStartMenuShortcut
        {
            get => _createStartMenuShortcut;
            set { _createStartMenuShortcut = value; OnPropertyChanged(); }
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

        public bool IsEulaAccepted
        {
            get => _isEulaAccepted;
            set { _isEulaAccepted = value; OnPropertyChanged(); }
        }

        public string EulaText
        {
            get => _eulaText;
            set { _eulaText = value; OnPropertyChanged(); }
        }

        public bool CanGoBack => CurrentStage is "License" or "PathSelection";
        public bool CanGoNext => CurrentStage is "Welcome" or "License";

        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand StartInstallCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand LaunchAndFinishCommand { get; }

        private void GoNext()
        {
            switch (CurrentStage)
            {
                case "Welcome":
                    CurrentStage = "License";
                    break;
                case "License":
                    if (!IsEulaAccepted)
                    {
                        MessageBox.Show(
                            "Чтобы продолжить установку, необходимо принять лицензионное соглашение.",
                            "Лицензионное соглашение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                    CurrentStage = "PathSelection";
                    break;
            }
        }

        private void GoBack()
        {
            switch (CurrentStage)
            {
                case "License":
                    CurrentStage = "Welcome";
                    break;
                case "PathSelection":
                    CurrentStage = "License";
                    break;
            }
        }

        private void LaunchAndFinish()
        {
            string exePath = Path.Combine(InstallPath, _manifest.ExecutableName);

            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Исполняемый файл не найден:\n{exePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = InstallPath,
                    Verb = "open"
                };

                using var process = Process.Start(psi);

                // Даём процессу время на инициализацию перед закрытием установщика
                if (process != null)
                {
                    process.WaitForInputIdle(3000);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить MuseumSpace:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Application.Current.Shutdown();
        }

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

        private void LoadEula()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("MuseumSpaceInstaller.Resources.eula.txt");
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    EulaText = reader.ReadToEnd();
                }
                else
                {
                    EulaText = "Лицензионное соглашение не найдено. Обратитесь к разработчику.";
                }
            }
            catch
            {
                EulaText = "Не удалось загрузить лицензионное соглашение.";
            }
        }

        private void CheckSystem()
        {
            _isUpdate = SystemChecker.IsInstalled(
                _manifest.ProductCode,
                _manifest.ApplicationName,
                out _existingVersion,
                out _existingInstallPath);

            if (_isUpdate)
            {
                var comparison = CompareVersions(_existingVersion ?? "0.0.0", _manifest.Version);
                if (comparison == 0)
                {
                    VersionComparisonResult = $"У вас уже установлена актуальная версия программы ({_existingVersion}).";
                }
                else if (comparison > 0)
                {
                    VersionComparisonResult = $"На компьютере установлена более новая версия ({_existingVersion}).\nПродолжить установку более старой версии?";
                }
                else
                {
                    VersionComparisonResult = $"Будет выполнено обновление с версии {_existingVersion} до {_manifest.Version}.";
                    if (!string.IsNullOrEmpty(_existingInstallPath))
                        InstallPath = _existingInstallPath;
                }
            }
            else
            {
                VersionComparisonResult = $"Будет установлена версия {_manifest.Version}.";
            }

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

            long freeSpace = SystemChecker.GetFreeSpaceBytes(InstallPath);
            long requiredBytes = (long)_manifest.RequiredSpaceMB * 1024 * 1024;
            if (freeSpace < requiredBytes)
            {
                MessageBox.Show($"Недостаточно свободного места на диске.\nТребуется: {_manifest.RequiredSpaceMB} МБ\nДоступно: {freeSpace / 1024 / 1024} МБ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (SystemChecker.IsSystemDirectory(InstallPath))
            {
                MessageBox.Show("Установка в системные каталоги запрещена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_isUpdate)
            {
                var comparison = CompareVersions(_existingVersion ?? "0.0.0", _manifest.Version);
                if (comparison == 0)
                {
                    var res = MessageBox.Show("У вас уже установлена актуальная версия программы.\nПереустановить?", "Версия совпадает", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.No) return;
                }
                else if (comparison > 0)
                {
                    var res = MessageBox.Show("На компьютере установлена более новая версия программы.\nПродолжить установку более старой версии?", "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.No) return;
                }
            }

            IsInstalling = true;
            CurrentStage = "Installing";

            InstallationLog.Clear();

            var engine = new InstallationEngine(
                _manifest,
                InstallPath,
                CreateDesktopShortcut,
                CreateStartMenuShortcut,
                progress =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentStage = progress.Stage;
                        ProgressPercent = progress.ProgressPercent;
                        ProgressDetail = progress.Detail;
                        IsIndeterminate = progress.IsIndeterminate;
                    });
                },
                line =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        InstallationLog.Add(line);
                        while (InstallationLog.Count > 200) InstallationLog.RemoveAt(0);
                    });
                });

            var cts = new CancellationTokenSource();
            bool success = await engine.InstallAsync(cts.Token);

            IsInstalling = false;

            if (success)
            {
                CurrentStage = "Finish";
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