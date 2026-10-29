namespace IntoChat.OS.Modules.ArtCraft;

[DigitalBrain.Module(ManifestName)]
public sealed class ArtCraftModule : DigitalBrain.Module
{
    internal const string ManifestName = "artcraft";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<ArtCraftModuleOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(ManifestName))],
        Secrets: []);

    public static bool RequiresContainer => true;

    public static System.Reflection.Assembly? Contracts => typeof(IArtCraftStudio).Assembly;

    // The studio neuron reads its options; the apps themselves run as containers the hosting adds.
}
