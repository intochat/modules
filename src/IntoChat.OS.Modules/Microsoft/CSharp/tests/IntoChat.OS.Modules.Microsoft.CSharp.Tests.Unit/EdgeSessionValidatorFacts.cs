using System.Security.Cryptography;
using DigitalBrain.Capabilities;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Edge;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class EdgeSessionValidatorFacts : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "os-edge-validator-" + Guid.NewGuid().ToString("N"));
    private readonly TestTimeProvider _time = new();

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    [Fact]
    public async Task ValidTokenParsesCorrectly()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain1", "todo-app", "sha256_hash", "account_xyz");
        var claims = validator.Validate("brain1", token);

        Assert.Equal("brain1", claims.Brain);
        Assert.Equal("todo-app", claims.App);
        Assert.Equal("sha256_hash", claims.Sha);
        Assert.Equal("account_xyz", claims.AccountId);
        Assert.True(claims.Expires > _time.GetUtcNow());
    }

    [Fact]
    public async Task ExpiredTokenIsRejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain1", "todo-app", "sha256_hash");

        // Advance time beyond token lifetime
        _time.Now += SessionTokens.Lifetime + TimeSpan.FromMinutes(5);

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain1", token));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedSignatureIsRejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain1", "todo-app", "sha256_hash");
        var parts = token.Split('.');
        Assert.Equal(2, parts.Length);

        // Tamper with signature
        var tamperedSignature = parts[1].EndsWith('A') ? parts[1][..^1] + "B" : parts[1][..^1] + "A";
        var tamperedToken = parts[0] + "." + tamperedSignature;

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain1", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TamperedPayloadIsRejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain1", "todo-app", "sha256_hash");
        var parts = token.Split('.');

        // Tamper with payload
        var tamperedPayload = parts[0].EndsWith('A') ? parts[0][..^1] + "B" : parts[0][..^1] + "A";
        var tamperedToken = tamperedPayload + "." + parts[1];

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain1", tamperedToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MismatchedBrainIsRejected()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var token = tokens.IssueAppToken("brain1", "todo-app", "sha256_hash");

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain2", token));
        Assert.Contains("speaks for another brain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-format")]
    [InlineData("a.b.c")]
    public async Task MissingOrMalformedTokenIsRejected(string? invalidToken)
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var ex = Assert.Throws<UnauthorizedAccessException>(() => validator.Validate("brain1", invalidToken));
        Assert.Contains("missing, invalid or expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallerStampingProducesCorrectContext()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var claims = new AppSessionClaims("brain1", "todo-app", "sha_123", _time.GetUtcNow() + TimeSpan.FromHours(1), "account_cust");
        var caller = validator.Stamp(claims);

        Assert.Equal(CallerKind.App, caller.Kind);
        Assert.Equal(AppPrincipal.Id("todo-app"), caller.PrincipalId);
        Assert.Equal("todo-app", caller.AppId);
        Assert.Equal("brain1", caller.BrainId);
        Assert.Equal("account_cust", caller.AccountId);

        Assert.True(CallerContextStamper.TryGet(out var stamped));
        Assert.Equal(caller, stamped);
    }

    [Fact]
    public async Task CallerStampingDefaultsAccountIdToBrainWhenNull()
    {
        var tokens = CreateTokens();
        await tokens.InitializeAsync(CancellationToken.None);
        var validator = new EdgeSessionValidator(tokens, _time);

        var claims = new AppSessionClaims("workspace-42", "gallery", "sha_456", _time.GetUtcNow() + TimeSpan.FromHours(1), null);
        var caller = validator.Stamp(claims);

        Assert.Equal("workspace-42", caller.AccountId);
        Assert.Equal("workspace-42", caller.BrainId);
        Assert.Equal(CallerKind.App, caller.Kind);
        Assert.Equal("gallery", caller.AppId);
    }

    private SessionTokens CreateTokens()
    {
        var values = new Dictionary<string, string?>
        {
            [IntoChatOSNames.HomeKey] = _home,
            [IntoChatOSNames.TopologyKey] = IntoChatOSNames.TopologyLocal,
        };

        return new SessionTokens(
            new IdleIntegrations(),
            _time,
            new Env(Environments.Development),
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new Quiet());
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
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
}
