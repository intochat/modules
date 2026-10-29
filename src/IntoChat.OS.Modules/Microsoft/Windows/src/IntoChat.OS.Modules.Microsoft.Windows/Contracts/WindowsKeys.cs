namespace IntoChat.OS.Modules.Microsoft.Windows;

// A machine's files and console live under its connection key.
public static class WindowsKeys
{
    public static string Files(string connectionKey) => connectionKey + "/files";

    public static string Console(string connectionKey) => connectionKey + "/console";

    public static string Connection(string childKey) => childKey[..childKey.LastIndexOf('/')];
}
