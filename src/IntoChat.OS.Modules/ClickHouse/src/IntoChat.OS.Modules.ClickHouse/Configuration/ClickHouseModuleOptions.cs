using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.ClickHouse;

public sealed class ClickHouseModuleOptions : IModuleOptions
{
    public string? Provider { get; set; } = "ClickHouse";
    public string ConnectionName { get; set; } = "clickhouse";
    public string DatabaseName { get; set; } = "intochat_os";
    public ClickHouseResourceOptions Hosting { get; set; } = new();

    public ClickHouseModuleOptions WithClickHouse(Action<ClickHouseResourceOptions>? configure = null)
    {
        Provider = "ClickHouse";
        Hosting.Enabled = true;
        configure?.Invoke(Hosting);
        return this;
    }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(DatabaseName);
        if (!string.Equals(Provider, "ClickHouse", StringComparison.OrdinalIgnoreCase))
        { throw new ArgumentException("ClickHouse requires the ClickHouse provider."); }
        ArgumentNullException.ThrowIfNull(Hosting);
        if (Hosting.Seeds.Any(string.IsNullOrWhiteSpace))
        { throw new ArgumentException("ClickHouse seed names must not be blank."); }
    }


}

public sealed class ClickHouseResourceOptions
{
    public bool Enabled { get; set; }
    public bool PersistentStorage { get; set; } = true;
    public bool AlwaysRunInitScripts { get; set; }
    public List<string> Seeds { get; set; } = [];
    public ClickHouseResourceOptions WithSeed(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Seeds.Contains(name, StringComparer.Ordinal)) { Seeds.Add(name); }
        return this;
    }
}
