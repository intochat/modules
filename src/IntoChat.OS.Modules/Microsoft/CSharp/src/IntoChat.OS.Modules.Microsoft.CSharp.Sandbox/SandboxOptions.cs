namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";

    // Downloaded assemblies, by content hash; shared by every session in this process.
    public string CacheRoot { get; set; } = "/cache";
    public int LogLines { get; set; } = 5000;
    // How long a reassign waits for the old run's in-flight work before it gives the update up and keeps the old run.
    public TimeSpan DrainDeadline { get; set; } = TimeSpan.FromSeconds(30);
    // Dedicated threads each session may spend on rules; a hung rule holds its thread for good.
    public int RuleThreads { get; set; } = 2;
}
