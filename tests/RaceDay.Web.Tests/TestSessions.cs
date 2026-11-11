using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

internal static class TestSessions
{
    public static (ApiSessionContext Context, MemorySession Session) Create()
    {
        var session = new MemorySession();
        return (new ApiSessionContext(session), session);
    }

    internal sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;

        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value.ToArray();
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
        {
            if (_values.TryGetValue(key, out var storedValue))
            {
                value = storedValue;
                return true;
            }

            value = null;
            return false;
        }
    }
}
