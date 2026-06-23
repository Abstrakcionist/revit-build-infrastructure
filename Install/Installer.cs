using Installer;
using RevitPlugin.Contracts;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;
using File = System.IO.File;


var pluginRoot = Environment.GetEnvironmentVariable("PLUGIN_ROOT")
                 ?? throw new InvalidOperationException("PLUGIN_ROOT is not set");

var infrastructureRoot = Environment.GetEnvironmentVariable("INFRASTRUCTURE_PATH")
                         ?? throw new InvalidOperationException("INFRASTRUCTURE_PATH is not set");

var configPath = Environment.GetEnvironmentVariable("PLUGIN_CONFIG_PATH")
                 ?? Path.Combine(pluginRoot, "plugin.config.json");

var config = PluginConfig.Load(configPath);
var outputName = config.Plugin.Name;
var projectName = config.Plugin.Name;

var versioning = Versioning.CreateFromVersionStringAsync(args[0]);
var project = new Project
{
    OutDir = Path.Combine(pluginRoot, config.Build.OutputDirectory),
    Name = projectName,
    Platform = Platform.x64,
    UI = WUI.WixUI_FeatureTree,
    MajorUpgrade = MajorUpgrade.Default,
    GUID = new Guid(config.Plugin.ProductGuid),
    Version = versioning.VersionPrefix,
    ControlPanelInfo =
    {
        Manufacturer = config.Plugin.Manufacturer ?? Environment.UserName
    }
};

ApplyOptionalInstallerAsset(config.ResolvePath(
    pluginRoot,
    infrastructureRoot,
    config.Install?.BannerImage,
    @"Install\Resources\Icons\BannerImage.png"), asset => project.BannerImage = asset);

ApplyOptionalInstallerAsset(config.ResolvePath(
    pluginRoot,
    infrastructureRoot,
    config.Install?.BackgroundImage,
    @"Install\Resources\Icons\BackgroundImage.png"), asset => project.BackgroundImage = asset);

ApplyOptionalInstallerAsset(config.ResolvePath(
    pluginRoot,
    infrastructureRoot,
    config.Install?.ProductIcon,
    @"Install\Resources\Icons\ShellIcon.ico"), asset => project.ControlPanelInfo.ProductIcon = asset);

var wixEntities = Generator.GenerateWixEntities(args[1..]);
project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.CustomizeDlg);

BuildSingleUserMsi();
BuildMultiUserUserMsi();

void BuildSingleUserMsi()
{
    project.Scope = InstallScope.perUser;
    project.OutFileName = $"{outputName}-{versioning.Version}-SingleUser";
    project.Dirs =
    [
        new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\", wixEntities)
    ];
    project.BuildMsi();
}

void BuildMultiUserUserMsi()
{
    project.Scope = InstallScope.perMachine;
    project.OutFileName = $"{outputName}-{versioning.Version}-MultiUser";
    project.Dirs =
    [
        new InstallDir(
            versioning.VersionPrefix.Major >= 2027
                ? @"%ProgramFiles%\Autodesk\Revit\Addins"
                : @"%CommonAppDataFolder%\Autodesk\Revit\Addins", wixEntities)
    ];
    project.BuildMsi();
}

static void ApplyOptionalInstallerAsset(string path, Action<string> assign)
{
    if (File.Exists(path))
    {
        assign(path);
    }
}
