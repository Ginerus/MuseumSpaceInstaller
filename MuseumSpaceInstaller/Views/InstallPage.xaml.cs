using MuseumSpaceInstaller.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MuseumSpaceInstaller.Views
{
    public partial class InstallPage : Page
    {
        public InstallPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is InstallerViewModel oldVm)
                oldVm.InstallationLog.CollectionChanged -= OnLogCollectionChanged;

            if (e.NewValue is InstallerViewModel newVm)
                newVm.InstallationLog.CollectionChanged += OnLogCollectionChanged;
        }

        private void OnLogCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(new Action(() => TerminalScroll.ScrollToEnd()),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }
}