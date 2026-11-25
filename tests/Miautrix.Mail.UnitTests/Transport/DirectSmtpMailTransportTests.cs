using System.Net;
using Miautrix.Mail.Application.Transport;
using Miautrix.Mail.Domain;
using Xunit;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class DirectSmtpMailTransportTests
{
    [Fact]
    public async Task Null_mx_is_a_permanent_failure()
    {
        var transport = new DirectSmtpMailTransport(new FakeDns([new MxHost("", 0)]),
            new DirectSmtpOptions { EhloName = "mail.example.test" }, new NoPolicyProvider());

        var result = await transport.SendAsync(new OutboundMessage(Guid.NewGuid(), "sender@sender.test",
            "user@recipient.test", "From: sender@sender.test\r\n\r\nbody"));

        Assert.False(result.IsSuccess);
        Assert.Equal(556, result.ResponseCode);
        Assert.Equal("direct", transport.Mode);
    }

    [Fact]
    public void Direct_is_a_known_per_domain_transport_mode()
    {
        Assert.True(DomainTransportModes.IsKnown(DomainTransportModes.Direct));
        Assert.Equal("cloudflare", DomainTransportModes.Cloudflare);
    }

    private sealed class FakeDns(IReadOnlyList<MxHost> mx) : IDnsResolver
    {
        public Task<IReadOnlyList<MxHost>> GetMxAsync(string domain, CancellationToken ct = default) => Task.FromResult(mx);
        public Task<IReadOnlyList<IPAddress>> GetAddressesAsync(string host, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IPAddress>>([]);
        public Task<IReadOnlyList<string>> GetTxtAsync(string name, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<string>> GetPtrAsync(IPAddress address, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoPolicyProvider : IMtaStsPolicyProvider
    {
        public Task<MtaStsPolicy?> GetPolicyAsync(string domain, CancellationToken ct = default) => Task.FromResult<MtaStsPolicy?>(null);
    }
}
