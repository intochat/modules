using IntoChat.OS.Framework.Machine;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Connections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.Windows;

internal sealed record WindowsRootInput(string? Path);

internal static class WindowsEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = BrainRoutes.Group(endpoints, "/windows");
        group.MapGet("/device", ([FromServices] IMachineIdentity identity) =>
            OperatingSystem.IsWindows()
                ? Results.Ok(new { machineId = identity.Id, machineName = Environment.MachineName })
                : Results.NotFound());
        group.MapGet("/{machineId}/roots", (string machineId, [FromServices] IMachineIdentity identity, [FromServices] IServiceProvider grains) =>
            WithMachine(machineId, identity, grains, async machine => Results.Ok(await machine.Roots())));
        group.MapPost("/{machineId}/roots/allow", (string machineId, WindowsRootInput input, [FromServices] IMachineIdentity identity, [FromServices] IServiceProvider grains) =>
            ChangeRoot(machineId, input, identity, grains, true));
        group.MapPost("/{machineId}/roots/revoke", (string machineId, WindowsRootInput input, [FromServices] IMachineIdentity identity, [FromServices] IServiceProvider grains) =>
            ChangeRoot(machineId, input, identity, grains, false));
    }

    private static Task<IResult> ChangeRoot(string machineId, WindowsRootInput input, IMachineIdentity identity, [FromServices] IServiceProvider grains, bool allow)
        => WithMachine(machineId, identity, grains, async machine =>
        {
            if (string.IsNullOrWhiteSpace(input.Path) || !Path.IsPathFullyQualified(input.Path))
            { return Results.BadRequest(new { error = "Enter a full folder path on the connected Windows computer." }); }
            try
            {
                if (allow) { await machine.AllowRoot(input.Path); }
                else { await machine.RevokeRoot(input.Path); }
                return Results.Ok(await machine.Roots());
            }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
            { return Results.BadRequest(new { error = "The folder is unavailable on the connected Windows computer." }); }
        });

    private static async Task<IResult> WithMachine(string machineId, IMachineIdentity identity, [FromServices] IServiceProvider grains, Func<IWindows, Task<IResult>> action)
    {
        if (!OperatingSystem.IsWindows() || machineId != identity.Id) { return Results.NotFound(); }
        var key = ConnectionKeys.For(BrainScope.CurrentId(), "windows", machineId);
        if ((await grains.GetConnection(key).Read()).Status != ConnectionStatus.Connected)
        { return Results.Conflict(new { error = "Connect this Windows computer before choosing allowed folders." }); }
        return await action(SiloNeurons.Get<IWindows>(grains, key));
    }
}
