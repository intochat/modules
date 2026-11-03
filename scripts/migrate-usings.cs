// Rewrites IntoChat.OS framework/contracts/connections usings and qualified references in modules/src
// to their DigitalBrain equivalents (Task 21).
using System.Text;
using System.Text.RegularExpressions;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var srcRoot = Path.Combine(repoRoot, "src");
var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"))
    .Order(StringComparer.Ordinal)
    .ToArray();

var (tokenRules, inlineReplacements) = BuildRules();
var modified = 0;

foreach (var file in files)
{
    var original = File.ReadAllText(file);
    var updated = RewriteFile(original, tokenRules, inlineReplacements);
    if (!string.Equals(original, updated, StringComparison.Ordinal))
    {
        File.WriteAllText(file, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        modified++;
    }
}

Console.WriteLine($"Processed {files.Length} files; updated {modified} files.");

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IntoChat.Modules.slnx")))
    {
        dir = dir.Parent;
    }
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

static (List<(string Namespace, Regex Pattern)> TokenRules, List<(string OldText, string NewText)> InlineReplacements) BuildRules()
{
    var inlineReplacements = new List<(string OldText, string NewText)>
    {
        ("IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration", "DigitalBrain.Modules.Configuration.ModuleConfiguration"),
        ("IntoChatOSConfiguration.", "ModuleConfiguration."),
        ("IntoChat.OS.Runtime.PlainNeuronContracts", "DigitalBrain.Contracts.PlainNeuronContracts"),
        ("IntoChat.OS.Connections.Platform.Connections.ConnectionGrains.IdFor", "DigitalBrain.Capabilities.Connections.ConnectionKeys.IdFor"),
        ("ConnectionGrains.IdFor", "ConnectionKeys.IdFor"),
        ("CallerContextStamper.", "RuntimeCallerContext."),
    };

    var tokenRules = new List<(string Namespace, Regex Pattern)>
    {
        ("DigitalBrain.Brains", WordRegex("BrainScope", "BrainKey", "CallerAdmission", "IBrain", "BrainState")),
        ("DigitalBrain.Contracts", WordRegex("PlainNeuronContracts", "ConnectionContract", "ConnectionContractAttribute")),
        ("DigitalBrain.Errors", WordRegex("ServiceUnavailableException", "StaleActionException")),
        ("DigitalBrain.Log", WordRegex("LogSwallowed", "Swallow")),
        ("DigitalBrain.Modules.Configuration", WordRegex("ModuleConfiguration", "ConfigurationKeyGrammar")),
        ("DigitalBrain.Modules.Options", WordRegex("IModuleOptions", "SettingAttribute", "GetModuleOptions", "ModuleOptionsSerialization", "ModuleId", "ModuleSettingsValidation")),
        ("DigitalBrain.Runtime.Http.Surface", WordRegex("IHttpModule", "IHttpSurface", "IBrainAccess", "BrainRoutes", "HttpProblems", "IpThrottleFilter", "IpThrottleOptions", "ForwardedHeadersSetup", "ReturnUrlValidator", "IpPrefix")),
        ("DigitalBrain.Runtime.Orleans.Capabilities", WordRegex("RuntimeCallerContext", "UntrustedCallerException")),
        ("DigitalBrain.Capabilities.Compute", WordRegex("ICostMeter", "IPriceList", "CostKind", "CostSummary", "CostRow", "Funding", "CostSubject", "PriceQuote", "ExternalOperationDescriptor", "ExternalCallRequest", "ExternalCallHandle", "ExternalCallOutcome", "ExternalCallCompletion", "ExternalCallContext", "ExternalCallMetadata", "ExternalCallHttp", "ExternalCallHandler", "IExternalCallContextAccessor", "BudgetExhaustedException", "ComputeTracePage", "TraceContext", "AddMeteredHttpClient", "MeteredHttpClients", "AddModulePriceList", "ModulePriceListRegistration", "ModulePriceListRegistry", "ModulePriceLists")),
        ("DigitalBrain.Capabilities.Connections", WordRegex("IConnection", "IConnectionInbound", "ConnectionView", "OAuthGrant", "Credential", "ConnectionStatus", "ConnectionProbeOutcome", "ConnectionProbeResult", "ConnectionKeys", "ConnectionState", "ConnectionNeuron", "ConnectionNeuronTypes", "IConnectionIndex", "IBrainOwnerDispatch", "ConnectionConnected", "ConnectionDisconnected", "ConnectionExpired", "ConnectionFailing", "ConnectionUpdated")),
        ("DigitalBrain.Capabilities.Connections.Connectors", WordRegex("ConnectorDefinition", "OAuthMethod", "OAuthExternalOperations", "OAuthTokenExchangeException", "OAuthTokenFailure", "IntegrationSchema", "HostConfiguration", "ConnectionString", "IntegrationValue", "IsModuleOptionSet")),
        ("DigitalBrain.Capabilities.Connections.Integrations", WordRegex("IModuleIntegrations", "IIntegrationHandle", "IIntegrationStartup", "IntegrationSnapshot", "IntegrationStatus", "ConfigureIntegration", "ReleasedIntegration", "ICapabilitySource", "IntegrationCapability", "IRuntimeCapabilitySource", "NeuronType", "NeuronTypeHit")),
        ("DigitalBrain.Capabilities.Connections.Requests", WordRegex("IConnectionRequests", "ConnectionRequest")),
        ("DigitalBrain.Capabilities.Connections.Sealing", WordRegex("ISecretSealer", "IBrainSecretSealer", "BrainSeal")),
        ("DigitalBrain.Capabilities.Connections.Broker", WordRegex("ICapabilityConnections", "BrainConnection")),
        ("DigitalBrain.Identity.Local.Custody", WordRegex("IMasterKeyCustody", "MasterKeyCustodyKind", "FileMasterKeyCustody")),
    };

    return (tokenRules, inlineReplacements);
}

static Regex WordRegex(params string[] words)
    => new(@"\b(?:" + string.Join('|', words.Select(Regex.Escape)) + @")\b", RegexOptions.Compiled);

static string RewriteFile(
    string content,
    List<(string Namespace, Regex Pattern)> tokenRules,
    List<(string OldText, string NewText)> inlineReplacements)
{
    var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();

    var hadOsUsing = false;
    var hadComputeUsing = false;
    var removedCount = 0;

    for (var i = lines.Count - 1; i >= 0; i--)
    {
        var trimmed = lines[i].Trim();
        if (IsOsUsing(trimmed, out var isCompute))
        {
            hadOsUsing = true;
            hadComputeUsing |= isCompute;
            lines.RemoveAt(i);
            removedCount++;
        }
    }

    var body = string.Join('\n', lines);
    var changedInline = false;
    foreach (var (oldText, newText) in inlineReplacements)
    {
        if (body.Contains(oldText, StringComparison.Ordinal))
        {
            body = body.Replace(oldText, newText, StringComparison.Ordinal);
            changedInline = true;
        }
    }

    if (!hadOsUsing && !changedInline && removedCount == 0)
    {
        return content;
    }

    lines = body.Split('\n').ToList();
    var existingUsings = new HashSet<string>(
        lines.Select(l => l.Trim()).Where(l => l.StartsWith("using ", StringComparison.Ordinal) && l.EndsWith(';')),
        StringComparer.Ordinal);

    var neededNamespaces = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var (ns, pattern) in tokenRules)
    {
        if (pattern.IsMatch(body))
        {
            neededNamespaces.Add(ns);
        }
    }

    if (hadComputeUsing && Regex.IsMatch(body, @"\b(?:Cost|Trace)\b"))
    {
        neededNamespaces.Add("DigitalBrain.Capabilities.Compute");
    }

    if (Regex.IsMatch(body, @"\[\s*Setting\s*\("))
    {
        neededNamespaces.Add("DigitalBrain.Modules.Options");
    }

    var newUsingLines = neededNamespaces
        .Select(ns => $"using {ns};")
        .Where(u => !existingUsings.Contains(u))
        .ToList();

    if (newUsingLines.Count > 0)
    {
        var insertIndex = 0;
        while (insertIndex < lines.Count && lines[insertIndex].TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            insertIndex++;
        }
        lines.InsertRange(insertIndex, newUsingLines);
    }

    return string.Join(newline, lines);
}

static bool IsOsUsing(string trimmedLine, out bool isCompute)
{
    isCompute = false;
    if (!trimmedLine.StartsWith("using IntoChat.OS", StringComparison.Ordinal) || !trimmedLine.EndsWith(';'))
    {
        return false;
    }
    if (trimmedLine.StartsWith("using IntoChat.OS.Modules", StringComparison.Ordinal))
    {
        return false;
    }
    isCompute = trimmedLine is "using IntoChat.OS.Contracts.Compute;"
        or "using IntoChat.OS.Framework.Compute;"
        or "using IntoChat.OS.Framework.Modules.Metering;";
    return true;
}
