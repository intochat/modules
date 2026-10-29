using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Supabase;

public sealed class SupabaseModuleOptions : IModuleOptions
{
    public bool UseBackingStore { get; set; }
    public string Provider { get; set; } = "Npgsql";
    public string ConnectionName { get; set; } = "supabase";
    public SupabaseResourceOptions Hosting { get; set; } = new();

    public SupabaseModuleOptions WithConnection(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        UseBackingStore = true;
        ConnectionName = name;
        Hosting.Kind = SupabaseHostKind.External;
        return this;
    }

    public SupabaseModuleOptions WithPostgres()
    {
        UseBackingStore = true;
        Hosting.Kind = SupabaseHostKind.Postgres;
        return this;
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        if (!string.Equals(Provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
        { throw new ArgumentException("Supabase requires the Npgsql provider."); }
        ArgumentNullException.ThrowIfNull(Hosting);
        if (!Enum.IsDefined(Hosting.Kind)) { throw new ArgumentOutOfRangeException(nameof(Hosting), "Unknown Supabase host kind."); }
    }


}

public enum SupabaseHostKind { External, Postgres }

public sealed class SupabaseResourceOptions
{
    public SupabaseHostKind Kind { get; set; }
}
