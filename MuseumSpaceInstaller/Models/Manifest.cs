using Newtonsoft.Json;
using System.Collections.Generic;

namespace MuseumSpaceInstaller.Models
{
    public class Manifest
    {
        [JsonProperty("applicationName")]
        public string ApplicationName { get; set; } = string.Empty;

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("publisher")]
        public string Publisher { get; set; } = string.Empty;

        [JsonProperty("executableName")]
        public string ExecutableName { get; set; } = string.Empty;

        [JsonProperty("defaultInstallPath")]
        public string DefaultInstallPath { get; set; } = string.Empty;

        [JsonProperty("requiredSpaceMB")]
        public int RequiredSpaceMB { get; set; }

        [JsonProperty("configFiles")]
        public List<string> ConfigFiles { get; set; } = new();

        [JsonProperty("architecture")]
        public ArchitectureInfo Architecture { get; set; } = new();

        [JsonProperty("shortcuts")]
        public ShortcutsInfo Shortcuts { get; set; } = new();

        [JsonProperty("registry")]
        public RegistryInfo Registry { get; set; } = new();

        [JsonProperty("upgradeCode")]
        public string UpgradeCode { get; set; } = string.Empty;

        [JsonProperty("productCode")]
        public string ProductCode { get; set; } = string.Empty;
    }

    public class ArchitectureInfo
    {
        [JsonProperty("x86")]
        public ArchDetails X86 { get; set; } = new();

        [JsonProperty("x64")]
        public ArchDetails X64 { get; set; } = new();
    }

    public class ArchDetails
    {
        [JsonProperty("libvlcPath")]
        public string LibVlcPath { get; set; } = string.Empty;

        [JsonProperty("dotnetRuntimeInstaller")]
        public string DotnetRuntimeInstaller { get; set; } = string.Empty;
    }

    public class ShortcutsInfo
    {
        [JsonProperty("desktop")]
        public bool Desktop { get; set; }

        [JsonProperty("startMenu")]
        public bool StartMenu { get; set; }
    }

    public class RegistryInfo
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("publisher")]
        public string Publisher { get; set; } = string.Empty;

        [JsonProperty("displayVersion")]
        public string DisplayVersion { get; set; } = string.Empty;

        [JsonProperty("installLocation")]
        public string InstallLocation { get; set; } = string.Empty;

        [JsonProperty("uninstallString")]
        public string UninstallString { get; set; } = string.Empty;

        [JsonProperty("displayIcon")]
        public string DisplayIcon { get; set; } = string.Empty;

        [JsonProperty("helpLink")]
        public string HelpLink { get; set; } = string.Empty;

        [JsonProperty("urlInfoAbout")]
        public string UrlInfoAbout { get; set; } = string.Empty;

        [JsonProperty("noModify")]
        public int NoModify { get; set; }

        [JsonProperty("noRepair")]
        public int NoRepair { get; set; }
    }
}