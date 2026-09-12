namespace MarsvinWebExample.Tests;

/// <summary>
/// Shares one MarsvinWebAppFactory (one in-process host, one env var
/// override, one MarsvinDb_WebTest database) across every test class in
/// this collection. Two IClassFixture&lt;MarsvinWebAppFactory&gt; instances
/// would each set the same environment variable and boot a second host
/// against the same database name at the same time.
/// </summary>
[CollectionDefinition("WebApp collection")]
public class WebAppCollection : ICollectionFixture<MarsvinWebAppFactory>;
