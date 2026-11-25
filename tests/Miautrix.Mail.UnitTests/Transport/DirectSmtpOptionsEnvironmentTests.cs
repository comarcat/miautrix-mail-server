using Miautrix.Mail.Application.Transport;
using Xunit;

namespace Miautrix.Mail.UnitTests.Transport;

/// <summary>
/// Existing deployments have no OUTBOUND_EHLO_NAME; the Worker must still start by falling back
/// to MIAUTRIX_HOSTNAME, while an explicit invalid value still fails at boot.
/// </summary>
[Collection(nameof(DirectSmtpOptionsEnvironmentTests))]
[CollectionDefinition(nameof(DirectSmtpOptionsEnvironmentTests), DisableParallelization = true)]
public sealed class DirectSmtpOptionsEnvironmentTests
{
    [Fact]
    public void Falls_back_to_worker_hostname_when_ehlo_name_is_unset()
    {
        WithEnvironment(null, "mail.example.test", () =>
            Assert.Equal("mail.example.test", DirectSmtpOptions.FromEnvironment().EhloName));
    }

    [Fact]
    public void Explicit_ehlo_name_wins_over_worker_hostname()
    {
        WithEnvironment("mx-out.example.test", "mail.example.test", () =>
            Assert.Equal("mx-out.example.test", DirectSmtpOptions.FromEnvironment().EhloName));
    }

    [Fact]
    public void Invalid_or_missing_names_fail_at_boot()
    {
        WithEnvironment("not a host name", "mail.example.test", () =>
            Assert.Throws<InvalidOperationException>(() => DirectSmtpOptions.FromEnvironment()));
        WithEnvironment(null, null, () =>
            Assert.Throws<InvalidOperationException>(() => DirectSmtpOptions.FromEnvironment()));
    }

    private static void WithEnvironment(string? ehloName, string? hostname, Action assert)
    {
        var previousEhlo = Environment.GetEnvironmentVariable("OUTBOUND_EHLO_NAME");
        var previousHost = Environment.GetEnvironmentVariable("MIAUTRIX_HOSTNAME");
        try
        {
            Environment.SetEnvironmentVariable("OUTBOUND_EHLO_NAME", ehloName);
            Environment.SetEnvironmentVariable("MIAUTRIX_HOSTNAME", hostname);
            assert();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OUTBOUND_EHLO_NAME", previousEhlo);
            Environment.SetEnvironmentVariable("MIAUTRIX_HOSTNAME", previousHost);
        }
    }
}
