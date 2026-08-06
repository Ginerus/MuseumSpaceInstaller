using MuseumSpaceInstaller.ViewModels;
using MuseumSpaceInstaller.Views;
using System.Windows;

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

            MainFrame.Navigate(new WelcomePage { DataContext = _viewModel });

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(InstallerViewModel.CurrentStage))
                    UpdatePage();
            };
        }

        private void UpdatePage()
        {
            switch (_viewModel.CurrentStage)
            {
                case "Welcome":
                    MainFrame.Navigate(new WelcomePage { DataContext = _viewModel });
                    break;
                case "License":
                    MainFrame.Navigate(new LicensePage { DataContext = _viewModel });
                    break;
                case "PathSelection":
                    MainFrame.Navigate(new PathSelectionPage { DataContext = _viewModel });
                    break;
                case "Installing":
                case "Ошибка":
                    MainFrame.Navigate(new InstallPage { DataContext = _viewModel });
                    break;
                case "Finish":
                case "Готово":
                    MainFrame.Navigate(new FinishPage { DataContext = _viewModel });
                    break;
            }
        }
    }
}