using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Framework.Modules.Metering;
using IntoChat.OS.Integrations;
using IntoChat.OS.Modules.Google.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Google.Drive;

// Google Drive storage, signed in through google-auth's Google OAuth integration.
// Its own connection keeps existing Gmail grants unchanged: adding a scope to them would force re-consent.
[DigitalBrain.Module(ModuleName)]
public sealed class DriveModule : DigitalBrain.Module
{
    public const string ModuleName = DriveScopes.Connector;

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [DigitalBrain.Capabilities.Capability.Storage.V1],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<DriveModuleOptions>(IntoChatOSConfiguration.ModuleOptionsSection(ModuleName))],
        Secrets: []);

    // The connector carries auth and neurons only; storage@1 is declared on the module's manifest (05-07, R4-01).
    public static ConnectorDefinition Connector { get; } = new(DriveScopes.Connector, "Google Drive", [GoogleAuth.Integration], [GoogleAuth.Integration])
    {
        Neurons = [typeof(ILegacyStorage)],
        // Verification of the restricted drive scope is still pending. Connections request drive.readonly.
        // Writes run only when the grant already includes DriveScopes.Write or DriveScopes.Files.
        OAuthMethods = [GoogleAuth.OAuth(GoogleAuth.Integration, [DriveScopes.Read])],
    };

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ModulePriceLists.AddModulePriceList<GooglePriceList>(services, ModuleName, [
            new ExternalOperationDescriptor(GoogleDriveOperations.About, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.List, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Metadata, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Media, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Export, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Create, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Update, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Copy, GoogleAuth.Integration, "request"),
            new ExternalOperationDescriptor(GoogleDriveOperations.Delete, GoogleAuth.Integration, "request"),
        ]);
        services.TryAddSingleton(configuration.GetModuleOptions<DriveModuleOptions>(ModuleName));
        GoogleAuth.Require(services, ModuleName);
    }
}

public static class GoogleDriveOperations
{
    public const string About = "google.drive.about.get";
    public const string List = "google.drive.files.list";
    public const string Metadata = "google.drive.files.get.metadata";
    public const string Media = "google.drive.files.get.media";
    public const string Export = "google.drive.files.export";
    public const string Create = "google.drive.files.create";
    public const string Update = "google.drive.files.update";
    public const string Copy = "google.drive.files.copy";
    public const string Delete = "google.drive.files.delete";
}

internal static class DriveScopes
{
    internal const string Connector = "google-drive";
    internal const string Read = "https://www.googleapis.com/auth/drive.readonly";
    internal const string Write = "https://www.googleapis.com/auth/drive";
    internal const string Files = "https://www.googleapis.com/auth/drive.file";

    internal static bool GrantsWrite(string? scopes)
        => (scopes ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(scope => scope is Write or Files);
}

[GenerateSerializer, Alias("db.google-drive.unavailable")]
public sealed class DriveUnavailableException(string message) : InvalidOperationException(message);
