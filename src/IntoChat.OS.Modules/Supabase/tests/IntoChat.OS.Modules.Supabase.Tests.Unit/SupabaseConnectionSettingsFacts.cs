using Npgsql;

namespace IntoChat.OS.Modules.Supabase.Tests.Unit;

public sealed class SupabaseConnectionSettingsFacts
{
    [Fact]
    public void AUriBecomesConnectionSettingsWithSslRequired()
    {
        var settings = SupabaseConnectionSettings.Parse("postgresql://user:p%40ss@db.example.test:6543/app?application_name=ino");

        Assert.Equal("db.example.test", settings.Host);
        Assert.Equal(6543, settings.Port);
        Assert.Equal("app", settings.Database);
        Assert.Equal("user", settings.Username);
        Assert.Equal("p@ss", settings.Password);
        Assert.Equal(SslMode.Require, settings.SslMode);
        Assert.Equal("ino", settings.ApplicationName);
    }

    [Fact]
    public void TheUriQueryCanOverrideSslModeButTheSafetyCapsStayFixed()
    {
        var settings = SupabaseConnectionSettings.Parse("postgres://u:p@h/db?sslmode=verify-full&connect_timeout=3");

        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
        Assert.Equal(10, settings.Timeout); // the post-parse cap wins over connect_timeout
        Assert.Equal(15, settings.CommandTimeout);
        Assert.False(settings.IncludeErrorDetail);
        Assert.False(settings.LogParameters);
        Assert.False(settings.PersistSecurityInfo);
        Assert.Equal(0, settings.MaxAutoPrepare);
        Assert.Equal(5432, settings.Port);
    }

    [Fact]
    public void AnNpgsqlConnectionStringIsAccepted()
    {
        var settings = SupabaseConnectionSettings.Parse("Host=h;Database=d;Username=u;Password=p");

        Assert.Equal("h", settings.Host);
        Assert.Equal("d", settings.Database);
    }

    [Theory]
    [InlineData("Database=d;Username=u;Password=very-secret")]
    [InlineData("postgres://u:very-secret@h/db?sslmode=bogus")]
    [InlineData("postgres://u:very-secret@h/db?broken")]
    public void InvalidCredentialsAreRejectedWithoutLeakingTheSecret(string connection)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SupabaseConnectionSettings.Parse(connection));

        Assert.DoesNotContain("very-secret", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }
}
