using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.ArtCraft;

// Where each app's MCP server (Streamable HTTP) and web build are served. Hosted apps use fixed loopback
// ports so the brain, the Playwright app origins and a person's browser all name the same URL.
public sealed class ArtCraftModuleOptions : IModuleOptions
{
    public const int PhotoCraftMcpPort = 18100;
    public const int FilmCraftMcpPort = 18101;
    public const int PhotoCraftWebPort = 18110;
    public const int FilmCraftWebPort = 18111;

    public static string[] HostedOrigins => [$"http://localhost:{PhotoCraftWebPort}", $"http://localhost:{FilmCraftWebPort}"];

    public bool Host { get; set; }
    public string? PhotoCraftMcp { get; set; }
    public string? FilmCraftMcp { get; set; }
    public string? PhotoCraftWeb { get; set; }
    public string? FilmCraftWeb { get; set; }

    // When set (e.g. ghcr.io/intochat), the apps run the prebuilt os-artcraft-<app>-<mcp|web> images tagged with the
    // first 12 characters of the pinned ref; null builds the Dockerfiles locally.
    public string? ImageRegistry { get; set; }

    public ArtCraftModuleOptions WithHostedApps()
    {
        Host = true;
        PhotoCraftMcp = $"http://localhost:{PhotoCraftMcpPort}/mcp";
        FilmCraftMcp = $"http://localhost:{FilmCraftMcpPort}/mcp";
        PhotoCraftWeb = $"http://localhost:{PhotoCraftWebPort}/";
        FilmCraftWeb = $"http://localhost:{FilmCraftWebPort}/";
        return this;
    }

    public void Validate()
    {
        foreach (var url in new[] { PhotoCraftMcp, FilmCraftMcp, PhotoCraftWeb, FilmCraftWeb })
        {
            if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            { throw new ArgumentException($"ArtCraft endpoint '{url}' must be an absolute http(s) URL."); }
        }
    }

    internal (string Title, string? Mcp, string? Web) For(string app) => app switch
    {
        "photocraft" => ("PhotoCraft", PhotoCraftMcp, PhotoCraftWeb),
        "filmcraft" => ("FilmCraft", FilmCraftMcp, FilmCraftWeb),
        _ => throw new ArgumentException($"'{app}' is not an ArtCraft app; use photocraft or filmcraft.", nameof(app)),
    };
}
