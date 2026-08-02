using MuseumSpaceInstaller.ViewModels;
using MuseumSpaceInstaller.Views;
using System.Windows;
using System.Windows.Controls;

namespace MuseumSpaceInstaller
{
    public partial class MainWindow : Window
    {
        private readonly InstallerViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new InstallerViewModel();
            DataContext = _viewModel;

            // Навигация по стадиям
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InstallerViewModel.CurrentStage))
                {
                    UpdatePage();
                }
            };

            UpdatePage();
        }

        private void UpdatePage()
        {
            switch (_viewModel.CurrentStage)
            {
                case "Готово":
                    MainFrame.Navigate(new FinishPage { DataContext = _viewModel });
                    break;
                case "Установка":
                case "Ошибка":
                    MainFrame.Navigate(new InstallPage { DataContext = _viewModel });
                    break;
                default:
                    MainFrame.Navigate(new WelcomePage { DataContext = _viewModel });
                    break;
            }
        }
    }
}