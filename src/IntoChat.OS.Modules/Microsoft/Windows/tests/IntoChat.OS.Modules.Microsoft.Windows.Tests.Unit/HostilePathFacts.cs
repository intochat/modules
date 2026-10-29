using System.Text.Json;
using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Unit;

public sealed class HostilePathFacts
{
    public static IEnumerable<object[]> Corpus()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "hostile-paths.json");
        var rows = JsonSerializer.Deserialize<Row[]>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.True(rows.Length >= 40, $"corpus has {rows.Length} rows");
        foreach (var row in rows) { yield return [row]; }
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void EachCorpusRow(Row row)
    {
        if (row.Kind == "realfs")
        {
            Real(row);
            return;
        }

        var flavor = row.Os == "unix" ? PathFlavor.Unix : PathFlavor.Windows;
        try
        {
            var full = PathConfinement.Resolve(row.Roots, row.Input, flavor);
            Assert.StartsWith("inside:", row.Expect);
            var relative = row.Expect["inside:".Length..];
            var expected = Expected(row.Roots[0], relative, flavor);
            var compare = flavor == PathFlavor.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.True(string.Equals(expected, full, compare), $"'{row.Input}' resolved to '{full}', expected '{expected}'");
        }
        catch (ConfinementDenied denied)
        {
            Assert.Equal("reject", row.Expect);
            Assert.Equal(ConfinementDenied.FixedMessage, denied.Message);
        }
    }

    [Fact]
    public void InjectedStateAndSecretsDirectoriesAreDenied()
    {
        var windows = OperatingSystem.IsWindows();
        var root = windows ? @"C:\" : "/";
        var state = windows ? @"D:\os-state" : "/os-state";
        var secrets = windows ? @"D:\os-secrets" : "/os-secrets";
        var flavor = windows ? PathFlavor.Windows : PathFlavor.Unix;
        // A root that contains the injected directory still cannot name it. On Windows D:\ is not C:\Windows.
        var held = windows ? @"D:\" : "/";
        var statePath = windows ? @"D:\os-state\brain" : "/os-state/brain";
        var secretPath = windows ? @"D:\os-secrets\key" : "/os-secrets/key";
        var stateDenied = Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([held], statePath, flavor, state, secrets));
        var secretDenied = Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([held], secretPath, flavor, state, secrets));
        Assert.Equal(ConfinementReason.HardDeny, stateDenied.Reason);
        Assert.Equal(ConfinementReason.HardDeny, secretDenied.Reason);
        _ = root;
    }

    private static void Real(Row row)
    {
        var host = OperatingSystem.IsWindows() ? "windows" : "unix";
        if (row.Os != "any" && row.Os != host)
        {
            Assert.Skip($"realfs row {row.Input} is {row.Os}");
            return;
        }

        var root = Directory.CreateTempSubdirectory("hostile").FullName;
        var outside = Path.Combine(Directory.GetParent(root)!.FullName, "hostile-outside");
        try
        {
            switch (row.Input)
            {
                case "$symlink-file":
                    Directory.CreateDirectory(outside);
                    File.WriteAllText(Path.Combine(outside, "secret.txt"), "x");
                    Link(() => File.CreateSymbolicLink(Path.Combine(root, "link.txt"), Path.Combine(outside, "secret.txt")));
                    Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([root], Path.Combine(root, "link.txt")));
                    break;
                case "$symlink-dir":
                    Directory.CreateDirectory(outside);
                    Link(() => Directory.CreateSymbolicLink(Path.Combine(root, "jump"), outside));
                    Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([root], Path.Combine(root, "jump", "secret.txt")));
                    break;
                case "$junction":
                    Directory.CreateDirectory(outside);
                    Link(() =>
                    {
                        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{Path.Combine(root, "jump")}\" \"{outside}\"")
                        { UseShellExecute = false, CreateNoWindow = true };
                        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("mklink did not start");
                        process.WaitForExit();
                        if (process.ExitCode != 0) { throw new InvalidOperationException("mklink failed"); }
                    });
                    Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([root], Path.Combine(root, "jump")));
                    break;
                case "$symlink-inside":
                    File.WriteAllText(Path.Combine(root, "kept.txt"), "x");
                    Link(() => File.CreateSymbolicLink(Path.Combine(root, "link.txt"), Path.Combine(root, "kept.txt")));
                    var followed = PathConfinement.Resolve([root], Path.Combine(root, "link.txt"));
                    Assert.Equal(PathConfinement.Normalize(Path.Combine(root, "kept.txt")), PathConfinement.Normalize(followed));
                    break;
                case "$dotdot":
                    Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([root], ".."));
                    break;
                case "$long":
                    var nested = root;
                    for (var i = 0; i < 4; i++) { nested = Path.Combine(nested, new string('n', 70)); }
                    Directory.CreateDirectory(nested);
                    var file = Path.Combine(nested, "long");
                    File.WriteAllText(file, "x");
                    var full = PathConfinement.Resolve([root], file);
                    Assert.True(full.Length > 300, $"path length {full.Length}");
                    Assert.EndsWith("long", full, StringComparison.Ordinal);
                    break;
                default:
                    Assert.Fail($"unknown realfs input {row.Input}");
                    break;
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            try { if (Directory.Exists(outside)) { Directory.Delete(outside, recursive: true); } } catch (IOException) { }
        }
    }

    private static void Link(Action create)
    {
        try { create(); }
        catch (IOException ex) { Assert.Skip($"links are not available: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { Assert.Skip($"links are not available: {ex.Message}"); }
        catch (InvalidOperationException ex) { Assert.Skip($"links are not available: {ex.Message}"); }
    }

    private static string Expected(string root, string relative, PathFlavor flavor)
    {
        var sep = flavor == PathFlavor.Windows ? '\\' : '/';
        var basePath = root.TrimEnd('\\', '/');
        if (flavor == PathFlavor.Windows && basePath.Length == 2 && basePath[1] == ':') { basePath += "\\"; }
        if (flavor == PathFlavor.Unix && basePath.Length == 0) { basePath = "/"; }
        var rel = flavor == PathFlavor.Windows ? relative.Replace('/', '\\') : relative.Replace('\\', '/');
        if (basePath.EndsWith(sep)) { return basePath + rel; }
        return basePath + sep + rel;
    }

    public sealed record Row(string Input, string[] Roots, string Os, string Kind, string Expect);
}
