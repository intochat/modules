using System.Security.Cryptography;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class SessionTokensFacts : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "os-session-key-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) { Directory.Delete(_home, recursive: true); }
    }

    [Fact]
    public async Task ProductionLocalMintsAKeyUnderHomeAndReusesIt()
    {
        var first = Tokens(Environments.Production, IntoChatOSNames.TopologyLocal);
        await first.InitializeAsync(CancellationToken.None);
        var token = first.Issue("brain", "session");
        Assert.True(File.Exists(Path.Combine(_home, "secrets", SessionTokens.KeyFileName)));

        var second = Tokens(Environments.Production, IntoChatOSNames.TopologyLocal);
        await second.InitializeAsync(CancellationToken.None);
        var claims = second.Validate(token);
        Assert.Equal("brain", claims?.Brain);
        Assert.Equal("session", claims?.Session);
    }

    [Fact]
    public async Task AnUnsetTopologyIsLocal()
    {
        var tokens = Tokens(Environments.Production, topology: null);
        await tokens.InitializeAsync(CancellationToken.None);
        Assert.NotNull(tokens.Validate(tokens.Issue("brain", "session")));
        Assert.True(File.Exists(Path.Combine(_home, "secrets", SessionTokens.KeyFileName)));
    }

    [Fact]
    public async Task ProductionCloudStillRequiresTheConfiguredKey()
    {
        var tokens = Tokens(Environments.Production, IntoChatOSNames.TopologyCloud);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.InitializeAsync(CancellationToken.None));
        Assert.Contains("RunTokenKey", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home, "secrets", SessionTokens.KeyFileName)));
    }

    [Fact]
    public async Task DevelopmentDoesNotPersistAKey()
    {
        var tokens = Tokens(Environments.Development, IntoChatOSNames.TopologyLocal);
        await tokens.InitializeAsync(CancellationToken.None);
        Assert.NotNull(tokens.Validate(tokens.Issue("brain", "session")));
        Assert.False(Directory.Exists(Path.Combine(_home, "secrets")));
    }

    [Fact]
    public async Task AProvisionedRunTokenKeyWinsAndWritesNoFile()
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokens = Tokens(Environments.Production, IntoChatOSNames.TopologyCloud, new ReadyIntegrations(secret));
        await tokens.InitializeAsync(CancellationToken.None);
        Assert.NotNull(tokens.Validate(tokens.Issue("brain", "session")));
        Assert.False(File.Exists(Path.Combine(_home, "secrets", SessionTokens.KeyFileName)));
    }

    private SessionTokens Tokens(string environment, string? topology, IModuleIntegrations<CSharpModule>? integrations = null)
    {
        var values = new Dictionary<string, string?> { [IntoChatOSNames.HomeKey] = _home };
        if (topology is not null) { values[IntoChatOSNames.TopologyKey] = topology; }
        return new SessionTokens(
            integrations ?? new IdleIntegrations(),
            TimeProvider.System,
            new Env(environment),
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new Quiet());
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Quiet : ILogger<SessionTokens>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class IdleIntegrations : IModuleIntegrations<CSharpModule>
    {
        public IIntegrationHandle Get(string integrationId) => new Idle();

        private sealed class Idle : IIntegrationHandle
        {
            public string IntegrationId => "csharp";
            public Task<IntegrationSnapshot> ReadAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(new IntegrationSnapshot { IntegrationId = "csharp", Status = IntegrationStatus.Unconfigured });
            public Task<string> ReadSecretAsync(string field, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException(field);
            public Task<ReleasedIntegration> ReleaseAsync(CancellationToken cancellationToken = default)
                => throw new InvalidOperationException();
        }
    }

    private sealed class ReadyIntegrations(string secret) : IModuleIntegrations<CSharpModule>
    {
        public IIntegrationHandle Get(string integrationId) => new Ready(secret);

        private sealed class Ready(string secret) : IIntegrationHandle
        {
            public string IntegrationId => "csharp";
            public Task<IntegrationSnapshot> ReadAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(new IntegrationSnapshot { IntegrationId = "csharp", Status = IntegrationStatus.Ready });
            public Task<string> ReadSecretAsync(string field, CancellationToken cancellationToken = default) => Task.FromResult(secret);
            public Task<ReleasedIntegration> ReleaseAsync(CancellationToken cancellationToken = default)
                => throw new InvalidOperationException();
        }
    }
}
