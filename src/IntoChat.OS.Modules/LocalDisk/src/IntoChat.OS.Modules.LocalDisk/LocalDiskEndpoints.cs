using DigitalBrain.Runtime.Orleans.Hosting;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IntoChat.OS.Modules.LocalDisk;

internal sealed record LocalDiskRootInput(string? Path);

internal static class LocalDiskEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = BrainRoutes.Group(endpoints, "/storage/local");
        group.MapGet("/roots", (string brainId, IServiceProvider services) => Roots(brainId, services));
        group.MapPost("/roots/allow", (string brainId, LocalDiskRootInput input, IServiceProvider services) => Change(brainId, input, services, allow: true));
        group.MapPost("/roots/revoke", (string brainId, LocalDiskRootInput input, IServiceProvider services) => Change(brainId, input, services, allow: false));
    }

    private static async Task<IResult> Roots(string brainId, IServiceProvider services)
        => Results.Ok(await Neuron(services, brainId).Roots().ConfigureAwait(false));

    private static async Task<IResult> Change(string brainId, LocalDiskRootInput input, IServiceProvider services, bool allow)
    {
        if (LocalDiskRoots.Syntax(input.Path) is { } syntax) { return Results.BadRequest(new { error = syntax }); }
        try
        {
            var neuron = Neuron(services, brainId);
            if (allow) { await neuron.AllowRoot(input.Path!).ConfigureAwait(false); }
            else { await neuron.RevokeRoot(input.Path!).ConfigureAwait(false); }
            return Results.Ok(await neuron.Roots().ConfigureAwait(false));
        }
        catch (Exception error) when (LocalDiskRoots.FailureText(error) is { } text)
        { return Results.BadRequest(new { error = text }); }
    }

    private static ILocalDisk Neuron(IServiceProvider services, string brainId)
        => SiloNeurons.Get<ILocalDisk>(services, new DigitalBrain.NeuronId(new DigitalBrain.BrainId(brainId), LocalDiskNeuron.TypeName, LocalDiskNeuron.KeyName));
}
