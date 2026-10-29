using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Validation;

namespace IntoChat.OS.Modules.Google.Drive;

public sealed record DriveModuleOptions : IModuleOptions
{
    public Uri Endpoint { get; set; } = new("https://www.googleapis.com/drive/v3/");

    public void Validate() => ModuleValidation.ThrowIfInvalid(this, new DriveOptionsValidator());
}

internal sealed class DriveOptionsValidator : IModuleValidator<DriveModuleOptions>
{
    public ValidationIssue[] Validate(DriveModuleOptions value)
    {
        var endpoint = value.Endpoint;
        return endpoint is null || !endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != "https" && (endpoint.Scheme != "http" || !endpoint.IsLoopback)) ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            !endpoint.AbsolutePath.EndsWith('/')
            ? [new(nameof(value.Endpoint), "uri", "Use an HTTPS base endpoint ending in '/'; HTTP is allowed only for loopback tests.")]
            : [];
    }
}
