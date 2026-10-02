using Microsoft.AspNetCore.Http;

namespace MarsvinWebExample.Tests;

/// <summary>
/// Minimal in-memory ISession for tests that need a guest's session without
/// spinning up the real ASP.NET Core session middleware (that needs a full
/// WebApplicationFactory host - see EndToEndCartTests for the tests that go
/// through the real thing instead). Good enough for SessionCartStore's own
/// unit tests and for giving a bare DefaultHttpContext a working
/// HttpContext.Session in a direct PageModel test (see TestAuth.Anonymous).
/// </summary>
internal sealed class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = [];

    public bool IsAvailable => true;
    public string Id => "fake-session";
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
