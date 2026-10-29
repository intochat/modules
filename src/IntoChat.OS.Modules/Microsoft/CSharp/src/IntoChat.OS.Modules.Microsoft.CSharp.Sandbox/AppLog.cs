namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// Where a hosted [App] class reports: the Sandbox captures a behavior's standard error into its run log.
internal static class AppLog
{
    public static void Log(string message) => Console.Error.WriteLine(message);
}
