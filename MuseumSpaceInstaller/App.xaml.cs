using MuseumSpaceInstaller.Models;
using MuseumSpaceInstaller.Services;
using System.Windows;

namespace MuseumSpaceInstaller
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Проверка режима удаления
            if (UninstallerHelper.IsUninstallMode())
            {
                var manifest = LoadManifestForUninstall();
                string installPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
                UninstallerHelper.RunUninstall(installPath, manifest);
                Shutdown();
                return;
            }
        }

        private Manifest LoadManifestForUninstall()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MuseumSpaceInstaller.Resources.manifest.json");
            if (stream != null)
            {
                using var reader = new System.IO.StreamReader(stream);
                var json = reader.ReadToEnd();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Manifest>(json) ?? new Manifest();
            }
            return new Manifest();
        }
    }
}