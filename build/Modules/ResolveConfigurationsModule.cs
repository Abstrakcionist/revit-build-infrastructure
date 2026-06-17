using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ModularPipelines.Context;
using ModularPipelines.Modules;
using RevitPlugin.Contracts;
using Shouldly;

namespace Build.Modules;

/// <summary>
///     Resolve solution configurations required to compile the add-in for all supported Revit versions.
/// </summary>
public sealed class ResolveConfigurationsModule : Module<string[]>
{
    protected override async Task<string[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var solutionModel = await LoadSolutionModelAsync(cancellationToken);
        var configurations = solutionModel.BuildTypes
            .Where(configuration => configuration.Contains("Release.R", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        configurations.ShouldNotBeEmpty("No solution configurations have been found");

        return configurations;
    }

    private static async Task<SolutionModel> LoadSolutionModelAsync(CancellationToken cancellationToken)
    {
        var solutionPath = PluginContext.Load().SolutionPath;

        if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            await using var slnxStream = File.OpenRead(solutionPath);
            return await SolutionSerializers.SlnXml.OpenAsync(slnxStream, cancellationToken);
        }

        await using var slnStream = File.OpenRead(solutionPath);
        return await SolutionSerializers.SlnFileV12.OpenAsync(slnStream, cancellationToken);
    }
}
