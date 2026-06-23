using System.Text;
using Build;
using Build.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.GitHub.Extensions;
using ModularPipelines.Modules;
using Octokit;
using RevitPlugin.Contracts;
using File = ModularPipelines.FileSystem.File;

namespace Build.Modules;

/// <summary>
///     Generate the changelog for publishing the add-in.
/// </summary>
[DependsOn<ResolveVersioningModule>]
public sealed class GenerateChangelogModule(IOptions<PublishOptions> publishOptions) : Module<string>
{
    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var versioning = versioningResult.ValueOrDefault!;

        if (string.IsNullOrEmpty(publishOptions.Value.ChangelogFile))
        {
            context.Logger.LogInformation("Changelog file not specified");
            return await GenerateReleaseNotesAsync(context, versioning);
        }

        var pluginRoot = PluginContext.Load().PluginRoot;
        var changelogFile = new File(Path.Combine(pluginRoot, publishOptions.Value.ChangelogFile));
        if (!changelogFile.Exists)
        {
            context.Logger.LogWarning("Changelog specified but not found");
            return await GenerateReleaseNotesAsync(context, versioning);
        }

        var changelog = await ParseChangelog(changelogFile, versioning.Version);
        if (changelog.Length == 0)
        {
            context.Logger.LogWarning("No version entry exists in the changelog: {Version}", versioning.Version);
            return await GenerateReleaseNotesAsync(context, versioning);
        }

        return changelog.ToString();
    }

    /// <summary>
    ///     Parse the changelog file to extract the entries for a specific version.
    /// </summary>
    private static async Task<StringBuilder> ParseChangelog(File changelogFile, string version)
    {
        const string separator = "# ";

        var isChangelogEntryFound = false;
        var changelog = new StringBuilder();

        await foreach (var line in changelogFile.ReadLinesAsync())
        {
            if (isChangelogEntryFound)
            {
                if (line.StartsWith(separator)) break;

                changelog.AppendLine(line);
                continue;
            }

            if (line.StartsWith(separator) && line.Contains(version))
            {
                isChangelogEntryFound = true;
            }
        }

        TrimEmptyLines(changelog);
        return changelog;
    }

    /// <summary>
    ///     Remove empty lines from the beginning and end of the changelog builder.
    /// </summary>
    private static void TrimEmptyLines(StringBuilder changelog)
    {
        if (changelog.Length == 0) return;

        var start = 0;
        var end = changelog.Length - 1;

        while (start < changelog.Length && (changelog[start] == '\r' || changelog[start] == '\n')) start++;
        while (end >= start && (changelog[end] == '\r' || changelog[end] == '\n')) end--;

        if (end < changelog.Length - 1)
        {
            changelog.Remove(end + 1, changelog.Length - (end + 1));
        }

        if (start > 0)
        {
            changelog.Remove(0, start);
        }
    }

    /// <summary>
    ///     Call the GitHub API to generate release notes for a specific version.
    /// </summary>
    private static async Task<string?> GenerateReleaseNotesAsync(IModuleContext context,
        ResolveVersioningResult versioning)
    {
        var repository = await PluginGitHubRepository.ResolveAsync(context);
        var repositoryId = PluginGitHubRepository.TryGetRepositoryId()
                           ?? (await context.GitHub().Client.Repository.Get(repository.Owner, repository.RepositoryName)).Id;
        var previousTagName = await ResolvePreviousTagNameAsync(context, repository, versioning);

        var releaseNotes = await context.GitHub().Client.Repository.Release.GenerateReleaseNotes(repositoryId,
            new GenerateReleaseNotesRequest(versioning.Version)
            {
                PreviousTagName = previousTagName
            });

        return releaseNotes.Body;
    }

    /// <summary>
    ///     Resolves a previous tag name that exists on GitHub.
    /// </summary>
    private static async Task<string?> ResolvePreviousTagNameAsync(IModuleContext context,
        PluginGitHubRepository repository, ResolveVersioningResult versioning)
    {
        var candidate = versioning.PreviousVersion;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var isHashedVersion = candidate.Length >= 40 &&
                              candidate.All(c => char.IsDigit(c) || c is >= 'a' and <= 'f');
        if (isHashedVersion)
        {
            return null;
        }

        var tagNames = (await context.GitHub().Client.Repository
                .GetAllTags(repository.Owner, repository.RepositoryName))
            .Select(tag => tag.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (tagNames.Count == 0)
        {
            return null;
        }

        if (tagNames.Contains(candidate))
        {
            return candidate;
        }

        var alternateTag = candidate.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? candidate[1..]
            : $"v{candidate}";

        if (tagNames.Contains(alternateTag))
        {
            return alternateTag;
        }

        context.Logger.LogWarning(
            "Previous tag '{PreviousTag}' was not found on GitHub. Release notes will be generated without it.",
            candidate);

        return null;
    }
}