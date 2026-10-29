namespace IntoChat.OS.Modules.AI.Models;

public sealed class ProviderUnavailableException : InvalidOperationException
{
    public ProviderUnavailableException(string integration, string status, string[] missing)
        : base($"Integration '{integration}' is {status}"
            + (missing.Length == 0 ? "" : $"; missing {string.Join(", ", missing)}")
            + $". An operator completes it through POST /integrations/{integration}/registration.")
    {
        Integration = integration;
        Status = status;
        Missing = missing;
    }

    public string Integration { get; }

    public string Status { get; }

    public string[] Missing { get; }
}

