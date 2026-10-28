using MarsvinWebExample.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;

namespace MarsvinWebExample.Tests.Data;

/// <summary>Where the JWT signing key comes from, and when the app refuses to start without one.</summary>
public class JwtSettingsTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(key is null ? [] : new Dictionary<string, string?> { ["Jwt:Key"] = key })
            .Build();

    [Fact]
    public void InProduction_WithoutAKey_TheAppRefusesToStart()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.From(Config(null), new FakeEnvironment("Production")));
    }

    [Fact]
    public void AKeyShorterThan32Bytes_IsRefusedEverywhere()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.From(Config("too-short"), new FakeEnvironment("Development")));
    }

    [Fact]
    public void InDevelopment_WithoutAKey_EachRunGetsItsOwnRandomOne()
    {
        var first = JwtSettings.From(Config(null), new FakeEnvironment("Development"));
        var second = JwtSettings.From(Config(null), new FakeEnvironment("Development"));

        Assert.NotEqual(first.SigningKey.Key, second.SigningKey.Key);
        Assert.Equal(32, first.SigningKey.Key.Length);
    }

    [Fact]
    public void AConfiguredKey_IsUsedAsItIs()
    {
        var key = new string('k', 40);

        var settings = JwtSettings.From(Config(key), new FakeEnvironment("Production"));

        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(key), settings.SigningKey.Key);
    }
}
