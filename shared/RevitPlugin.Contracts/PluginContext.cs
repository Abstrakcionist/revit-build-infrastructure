namespace RevitPlugin.Contracts;

public sealed class PluginContext
{
    public required string PluginRoot { get; init; }
    public required string InfrastructureRoot { get; init; }
    public required string SolutionPath { get; init; }
    public required string HostProjectPath { get; init; }
    public required string InstallerProjectPath { get; init; }
    public required string ConfigPath { get; init; }
    public required PluginConfig Config { get; init; }

    public static PluginContext Load()
    {
        var pluginRoot = Environment.GetEnvironmentVariable("PLUGIN_ROOT")
                         ?? throw new InvalidOperationException("PLUGIN_ROOT is not set");

        var infrastructureRoot = Environment.GetEnvironmentVariable("INFRASTRUCTURE_PATH")
                                 ?? Path.Combine(pluginRoot, ".infrastructure");

        var configPath = Environment.GetEnvironmentVariable("PLUGIN_CONFIG_PATH")
                         ?? Path.Combine(pluginRoot, "plugin.config.json");

        var config = PluginConfig.Load(configPath);

        var solutionFiles = Directory.GetFiles(pluginRoot, "*.sln");
        if (solutionFiles.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one .sln file in plugin root, found {solutionFiles.Length}.");
        }

        var hostProjectPath = Path.IsPathRooted(config.Build.HostProjectPath)
            ? config.Build.HostProjectPath
            : Path.Combine(pluginRoot, config.Build.HostProjectPath);

        var installerProjectPath = Path.Combine(infrastructureRoot, "Install", "Installer.csproj");
        if (!File.Exists(installerProjectPath))
        {
            installerProjectPath = Path.Combine(infrastructureRoot, "install", "Installer.csproj");
        }

        if (!File.Exists(installerProjectPath))
        {
            throw new FileNotFoundException(
                $"Installer project was not found in infrastructure repository: {installerProjectPath}");
        }

        return new PluginContext
        {
            PluginRoot = pluginRoot,
            InfrastructureRoot = infrastructureRoot,
            SolutionPath = solutionFiles[0],
            HostProjectPath = hostProjectPath,
            InstallerProjectPath = installerProjectPath,
            ConfigPath = configPath,
            Config = config
        };
    }
}
