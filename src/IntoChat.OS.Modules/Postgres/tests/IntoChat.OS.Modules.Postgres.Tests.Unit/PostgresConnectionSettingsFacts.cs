using Npgsql;

namespace IntoChat.OS.Modules.Postgres.Tests.Unit;

public sealed class PostgresConnectionSettingsFacts
{
    [Fact]
    public void AUriBecomesConnectionSettingsWithSslRequired()
    {
        var settings = PostgresConnectionSettings.Parse("postgresql://user:p%40ss@db.example.test:6543/app?application_name=ino");

        Assert.Equal("db.example.test", settings.Host);
        Assert.Equal(6543, settings.Port);
        Assert.Equal("app", settings.Database);
        Assert.Equal("user", settings.Username);
        Assert.Equal("p@ss", settings.Password);
        Assert.Equal(SslMode.Require, settings.SslMode);
        Assert.Equal("ino", settings.ApplicationName);
    }

    [Fact]
    public void TheSafetyCapsAreFixedWhateverTheCredentialSays()
    {
        var settings = PostgresConnectionSettings.Parse("Host=h;Database=d;Username=u;Password=p;Timeout=300;Command Timeout=300;Include Error Detail=true");

        Assert.Equal(10, settings.Timeout);
        Assert.Equal(15, settings.CommandTimeout);
        Assert.False(settings.IncludeErrorDetail);
        Assert.False(settings.LogParameters);
        Assert.False(settings.PersistSecurityInfo);
        Assert.Equal(0, settings.MaxAutoPrepare);
    }

    [Theory]
    [InlineData("Database=d;Username=u;Password=very-secret")]
    [InlineData("postgres://u:very-secret@h/db?sslmode=bogus")]
    [InlineData("postgres://u:very-secret@h/db?broken")]
    public void InvalidCredentialsAreRejectedWithoutLeakingTheSecret(string connection)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgresConnectionSettings.Parse(connection));

        Assert.DoesNotContain("very-secret", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }
}
