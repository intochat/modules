#!/usr/bin/env -S dotnet --
#:property PublishAot=false
// Rewrites every <PackageVersion> of a package family in Directory.Packages.props:
// `dotnet run scripts/bump-pins.cs -- --family <DigitalBrain|IntoChat.OS.Modules> --version <semver> [--props <path>]`
// Exits 0 when at least one pin changed, 2 when nothing changed, and 1 on invalid arguments.
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var root = AppContext.GetData("EntryPointFileDirectoryPath") as string is { } entry
    ? Path.GetFullPath(Path.Combine(entry, ".."))
    : Directory.GetCurrentDirectory();

string? family = null;
string? version = null;
var propsPath = Path.Combine(root, "Directory.Packages.props");

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--family" when i + 1 < args.Length:
            family = args[++i];
            break;
        case "--version" when i + 1 < args.Length:
            version = args[++i];
            break;
        case "--props" when i + 1 < args.Length:
            propsPath = Path.GetFullPath(args[++i]);
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
            return 1;
    }
}

if (family is not ("DigitalBrain" or "IntoChat.OS.Modules"))
{
    Console.Error.WriteLine($"Invalid --family '{family}'. Expected 'DigitalBrain' or 'IntoChat.OS.Modules'.");
    return 1;
}

if (version is null || !Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant))
{
    Console.Error.WriteLine($"Invalid --version '{version}'. Expected valid semver.");
    return 1;
}

if (!File.Exists(propsPath))
{
    Console.Error.WriteLine($"Props file not found: {propsPath}");
    return 1;
}

var document = XDocument.Load(propsPath);
var matchingIds = document.Descendants("PackageVersion")
    .Select(element => element.Attribute("Include")?.Value)
    .OfType<string>()
    .Where(id => MatchesFamily(id, family))
    .ToHashSet(StringComparer.Ordinal);

var original = File.ReadAllText(propsPath);
var changed = false;
var updated = Regex.Replace(
    original,
    @"(?<prefix><PackageVersion\s+Include=""(?<id>[^""]+)""\s+Version="")(?<version>[^""]+)(?<suffix>"")",
    match =>
    {
        var id = match.Groups["id"].Value;
        var currentVersion = match.Groups["version"].Value;
        if (!matchingIds.Contains(id) || string.Equals(currentVersion, version, StringComparison.Ordinal))
        {
            return match.Value;
        }

        changed = true;
        return string.Concat(match.Groups["prefix"].Value, version, match.Groups["suffix"].Value);
    },
    RegexOptions.CultureInvariant);

if (!changed)
{
    return 2;
}

File.WriteAllText(propsPath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
return 0;

static bool MatchesFamily(string id, string family) => family switch
{
    "DigitalBrain" => string.Equals(id, "DigitalBrain", StringComparison.Ordinal)
        || id.StartsWith("DigitalBrain.", StringComparison.Ordinal),
    "IntoChat.OS.Modules" => id.StartsWith("IntoChat.OS.Modules.", StringComparison.Ordinal),
    _ => false,
};
