using System.Net;
using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.UnitTests.Transport;

internal sealed class FakeDnsResolver : IDnsResolver
{
    public Dictionary<string, IReadOnlyList<string>> Txt { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<IPAddress>> Addresses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<MxHost>> Mx { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<string>> Ptr { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Timeout { get; set; }
    public bool WildcardAddressAnswers { get; set; } = true;

    public Task<IReadOnlyList<MxHost>> GetMxAsync(string domain, CancellationToken ct = default) => Resolve(Mx, domain);
    public Task<IReadOnlyList<IPAddress>> GetAddressesAsync(string host, CancellationToken ct = default)
    {
        if (Timeout) throw new TimeoutException("Fake DNS timeout");
        if (Addresses.TryGetValue(host, out var exact)) return Task.FromResult(exact);
        if (WildcardAddressAnswers && host.EndsWith(".example.test", StringComparison.OrdinalIgnoreCase) && !host.StartsWith("none", StringComparison.OrdinalIgnoreCase) && !host.StartsWith("missing", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("192.0.2.1")]);
        return Task.FromResult<IReadOnlyList<IPAddress>>([]);
    }
    public Task<IReadOnlyList<string>> GetTxtAsync(string name, CancellationToken ct = default) => Resolve(Txt, name);
    public Task<IReadOnlyList<string>> GetPtrAsync(IPAddress address, CancellationToken ct = default) => Resolve(Ptr, address.ToString());
    private Task<IReadOnlyList<T>> Resolve<T>(Dictionary<string, IReadOnlyList<T>> source, string key)
    {
        if (Timeout) throw new TimeoutException("Fake DNS timeout");
        return Task.FromResult(source.TryGetValue(key, out var result) ? result : (IReadOnlyList<T>)Array.Empty<T>());
    }
}
