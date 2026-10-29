using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DigitalBrain.Runtime.Orleans.Hosting;
using MicrosoftSdk = Microsoft;

namespace IntoChat.OS.Modules.Flutter;

// UI-kit neurons hold per-brain window data: a route's {name} addresses the part at "{brain}/ui/{name}".
public static class UiScope
{
    public static string Key(string scope, string name) => $"{scope}/ui/{name}";
}

internal static class UiHttp
{
    public static void MapGet<TNeuron, TState>(IEndpointRouteBuilder endpoints, string collection, Func<TNeuron, Task<TState>> read)
        where TNeuron : class
    {
        // The value neurons read here are plain controls (06-04), reached through the host's plain-contract seam.
        BrainRoutes.Group(endpoints, "/ui").MapGet($"/{collection}/{{name}}",
            async Task<IResult> (string name, [MicrosoftSdk.AspNetCore.Mvc.FromServices] IServiceProvider plain, CancellationToken cancellationToken) =>
                Results.Ok(await read(SiloNeurons.Get<TNeuron>(plain, UiScope.Key(BrainScope.CurrentId(), name))).WaitAsync(cancellationToken)));
    }

    public static void MapPost(IEndpointRouteBuilder endpoints, string template, Delegate handler)
        => BrainRoutes.Group(endpoints, "/ui").MapPost("/" + template, handler)
            .AddEndpointFilter(static async (context, next) =>
            {
                var name = context.HttpContext.Request.RouteValues["name"]?.ToString()
                    ?? throw new ArgumentException("A UI value name is required.");
                context.Arguments[0] = UiScope.Key(BrainScope.CurrentId(), name);
                return await next(context);
            });
}
