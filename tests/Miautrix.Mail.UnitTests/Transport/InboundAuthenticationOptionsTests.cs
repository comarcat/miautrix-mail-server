using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.UnitTests.Transport;

[Collection("ProcessEnvironment")]
public sealed class InboundAuthenticationOptionsTests
{
    [Fact]
    public void Mode_defaults_to_monitor_and_invalid_mode_fails_boot_validation()
    {
        var oldMode = Environment.GetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE");
        var oldHost = Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME");
        var oldTrustedAuthservs = Environment.GetEnvironmentVariable("MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS");
        try
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE", null);
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS", null);
            Environment.SetEnvironmentVariable("MIAUTRIX_HOSTNAME", "mx.unit.test");
            var defaults = InboundAuthenticationOptions.FromEnvironment();
            Assert.Equal(AuthenticationMode.Monitor, defaults.Mode);
            Assert.Empty(defaults.TrustedAuthservIds);
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS", "mx.cloudflare.net, trusted.example.test");
            Assert.Contains("mx.cloudflare.net", InboundAuthenticationOptions.FromEnvironment().TrustedAuthservIds);
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS", "mx.cloudflare.net; evil.test");
            Assert.Throws<InvalidOperationException>(() => InboundAuthenticationOptions.FromEnvironment());
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS", null);
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE", "unexpected");
            Assert.Throws<InvalidOperationException>(() => InboundAuthenticationOptions.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE", oldMode);
            Environment.SetEnvironmentVariable("MIAUTRIX_HOSTNAME", oldHost);
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_TRUSTED_AUTHSERV_IDS", oldTrustedAuthservs);
        }
    }
}
