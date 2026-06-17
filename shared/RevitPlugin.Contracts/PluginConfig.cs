using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitPlugin.Contracts;

public sealed class PluginConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public required PluginSection Plugin { get; init; }

    [JsonPropertyName("Build")]
    public required BuildSettings Build { get; init; }

    public InstallSection? Install { get; init; }

    public static PluginConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Plugin config was not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PluginConfig>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize plugin config: {path}");
    }

    public string ResolvePath(string pluginRoot, string infrastructureRoot, string? path, string defaultRelativePath)
    {
        var value = string.IsNullOrWhiteSpace(path) ? defaultRelativePath : path;

        if (Path.IsPathRooted(value))
        {
            return value;
        }

        var infrastructureCandidate = Path.Combine(infrastructureRoot, value);
        if (File.Exists(infrastructureCandidate))
        {
            return infrastructureCandidate;
        }

        var pluginCandidate = Path.Combine(pluginRoot, value);
        if (File.Exists(pluginCandidate))
        {
            return pluginCandidate;
        }

        return infrastructureCandidate;
    }
}

public sealed class PluginSection
{
    public required string Name { get; init; }
    public required string ProductGuid { get; init; }
    public string? Manufacturer { get; init; }

    [JsonPropertyName("RevitVersions")]
    public string[]? RevitVersions { get; init; }
}

public sealed class BuildSettings
{
    public string OutputDirectory { get; init; } = "output";
    public required string HostProjectPath { get; init; }
}

public sealed class InstallSection
{
    public string? BannerImage { get; init; }
    public string? BackgroundImage { get; init; }
    public string? ProductIcon { get; init; }
}
