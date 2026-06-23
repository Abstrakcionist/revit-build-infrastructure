using System.Text.RegularExpressions;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Git.Options;
using ModularPipelines.Options;
using RevitPlugin.Contracts;

namespace Build;

internal sealed record PluginGitHubRepository(string Owner, string RepositoryName)
{
    public string Identifier => $"{Owner}/{RepositoryName}";

    public static async Task<PluginGitHubRepository> ResolveAsync(IModuleContext context,
        CancellationToken cancellationToken = default)
    {
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        if (!string.IsNullOrWhiteSpace(repository))
        {
            return ParseIdentifier(repository);
        }

        var pluginRoot = PluginContext.Load().PluginRoot;
        var remoteResult = await context.Git().Commands.Remote(
            new GitRemoteOptions
            {
                Arguments = ["get-url", "origin"]
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = pluginRoot,
                ThrowOnNonZeroExitCode = false,
                LogSettings = CommandLoggingOptions.Silent
            }, cancellationToken);

        var remoteUrl = remoteResult.StandardOutput.Trim();
        if (!string.IsNullOrWhiteSpace(remoteUrl))
        {
            return ParseRemoteUrl(remoteUrl);
        }

        throw new InvalidOperationException(
            "Could not resolve the plugin GitHub repository. Set GITHUB_REPOSITORY or configure origin in the plugin repository.");
    }

    public static long? TryGetRepositoryId()
    {
        return long.TryParse(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY_ID"), out var repositoryId)
            ? repositoryId
            : null;
    }

    private static PluginGitHubRepository ParseIdentifier(string identifier)
    {
        var parts = identifier.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid GITHUB_REPOSITORY value: {identifier}");
        }

        return new PluginGitHubRepository(parts[0], parts[1]);
    }

    private static PluginGitHubRepository ParseRemoteUrl(string remoteUrl)
    {
        var match = Regex.Match(
            remoteUrl,
            @"[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not parse GitHub repository from remote url: {remoteUrl}");
        }

        return new PluginGitHubRepository(match.Groups["owner"].Value, match.Groups["repo"].Value);
    }
}
