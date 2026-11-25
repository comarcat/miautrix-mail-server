using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.UnitTests.Transport;

public sealed class InboundAuthenticationOptionsTests
{
    [Fact]
    public void Mode_defaults_to_monitor_and_invalid_mode_fails_boot_validation()
    {
        var oldMode = Environment.GetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE");
        var oldHost = Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME");
        try
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE", null);
            Environment.SetEnvironmentVariable("MIAUTRIX_HOSTNAME", "mx.unit.test");
            Assert.Equal(AuthenticationMode.Monitor, InboundAuthenticationOptions.FromEnvironment().Mode);
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE", "unexpected");
            Assert.Throws<InvalidOperationException>(() => InboundAuthenticationOptions.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MIAUTRIX_INBOUND_AUTH_MODE", oldMode);
            Environment.SetEnvironmentVariable("MIAUTRIX_HOSTNAME", oldHost);
        }
    }
}
