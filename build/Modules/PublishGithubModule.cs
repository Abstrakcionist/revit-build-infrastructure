using Build;
using Build.Options;
using EnumerableAsyncProcessor.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.FileSystem;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Git.Options;
using ModularPipelines.GitHub.Attributes;
using ModularPipelines.GitHub.Extensions;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Octokit;
using RevitPlugin.Contracts;
using Shouldly;

namespace Build.Modules;

/// <summary>
///     Publish the add-in to GitHub.
/// </summary>
[SkipIfNoGitHubToken]
[DependsOn<ResolveVersioningModule>]
[DependsOn<GenerateGitHubChangelogModule>]
[DependsOn<CreateInstallerModule>(Optional = true)]
public sealed class PublishGithubModule(IOptions<BuildOptions> buildOptions) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var pluginContext = PluginContext.Load();
        var pluginRepository = await PluginGitHubRepository.ResolveAsync(context, cancellationToken);
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var changelogResult = await context.GetModule<GenerateGitHubChangelogModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var changelog = changelogResult.ValueOrDefault!;

        var outputFolder = GetOutputFolder(pluginContext, buildOptions.Value);
        outputFolder.Exists.ShouldBeTrue($"Output directory was not found: {outputFolder.Path}");

        var targetFiles = outputFolder
            .GetFiles(file => file.Extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        targetFiles.ShouldNotBeEmpty("No MSI installers were found to create the Release");

        var newRelease = new NewRelease(versioning.Version)
        {
            Name = versioning.Version,
            Body = changelog,
            TargetCommitish = await GetPluginCommitShaAsync(context, pluginContext.PluginRoot, cancellationToken),
            Prerelease = versioning.IsPrerelease
        };

        var release = await context.GitHub().Client.Repository.Release
            .Create(pluginRepository.Owner, pluginRepository.RepositoryName, newRelease);
        await targetFiles
            .ForEachAsync(async file =>
            {
                var asset = new ReleaseAssetUpload
                {
                    ContentType = "application/x-binary",
                    FileName = file.Name,
                    RawData = file.GetStream()
                };

                context.Logger.LogInformation("Uploading asset: {Asset}", asset.FileName);

                await context.GitHub().Client.Repository.Release.UploadAsset(release, asset, cancellationToken);
            }, cancellationToken)
            .ProcessInParallel();

        context.Summary.KeyValue("Deployment", "GitHub", release.HtmlUrl);
    }

    protected override async Task OnFailedAsync(IModuleContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersioningModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var pluginRoot = PluginContext.Load().PluginRoot;

        await context.Git().Commands.Push(new GitPushOptions
        {
            Delete = true,
            Arguments = ["origin", versioning.Version]
        }, new CommandExecutionOptions
        {
            WorkingDirectory = pluginRoot,
            ThrowOnNonZeroExitCode = false
        }, cancellationToken);
    }

    private static Folder GetOutputFolder(PluginContext pluginContext, BuildOptions buildOptions)
    {
        var outputDirectory = buildOptions.OutputDirectory;
        if (!Path.IsPathRooted(outputDirectory))
        {
            outputDirectory = Path.Combine(pluginContext.PluginRoot, outputDirectory);
        }
        else
        {
            outputDirectory = Path.GetFullPath(outputDirectory);
        }

        return new Folder(outputDirectory);
    }

    private static async Task<string> GetPluginCommitShaAsync(IModuleContext context, string pluginRoot,
        CancellationToken cancellationToken)
    {
        var revisionResult = await context.Git().Commands.RevList(
            new GitRevListOptions
            {
                MaxCount = "1",
                Pretty = "format:%H",
                Arguments = ["HEAD"],
                NoCommitHeader = true
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = pluginRoot,
                LogSettings = CommandLoggingOptions.Silent
            }, cancellationToken);

        return revisionResult.StandardOutput.Trim();
    }
}