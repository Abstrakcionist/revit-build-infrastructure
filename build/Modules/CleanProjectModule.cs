using Build.Options;
using RevitPlugin.Contracts;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Conditions;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Clean projects and artifact directories.
/// </summary>
[SkipIf<IsCI>]
public sealed class CleanProjectModule(IOptions<BuildOptions> buildOptions) : SyncModule
{
    protected override void ExecuteModule(IModuleContext context, CancellationToken cancellationToken)
    {
        var pluginContext = PluginContext.Load();
        var rootDirectory = context.Git().RootDirectory;
        var outputDirectory = rootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        var buildOutputDirectories = rootDirectory
            .GetFolders(folder => folder.Name is "bin" or "obj")
            .Where(folder => !folder.Path.StartsWith(pluginContext.InfrastructureRoot, StringComparison.OrdinalIgnoreCase));

        foreach (var buildFolder in buildOutputDirectories)
        {
            buildFolder.Clean();
        }

        if (outputDirectory.Exists)
        {
            outputDirectory.Clean();
        }
    }
}
