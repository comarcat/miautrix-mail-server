using Miautrix.Mail.Application.Mail;

namespace Miautrix.Mail.UnitTests.Sieve;

public sealed class SieveSenderAuthenticationGateTests
{
    private readonly SieveSenderAuthenticationGate _gate = new("mail.example.test");

    [Theory]
    [InlineData("spf=pass smtp.mailfrom=sender.example.test", "sender@example.test", true)]
    [InlineData("dmarc=pass header.from=example.test", "sender@example.test", true)]
    [InlineData("dkim=pass header.d=example.test header.s=mail", "sender@example.test", true)]
    [InlineData("dkim=pass header.d=other.test header.s=mail", "sender@example.test", false)]
    [InlineData("spf=fail smtp.mailfrom=sender.example.test", "sender@example.test", false)]
    public void ChecksOnlyPassingAlignedResultsFromOwnAuthserv(string result, string sender, bool expected)
    {
        var headers = $"Authentication-Results: mail.example.test; {result}\r\nFrom: {sender}\r\n";
        Assert.Equal(expected, _gate.IsAuthenticated(headers, sender));
    }

    [Fact]
    public void MissingOrNonFirstOwnAuthenticationResultsDoesNotAuthenticate()
    {
        Assert.False(_gate.IsAuthenticated("From: sender@example.test\r\n", "sender@example.test"));
        Assert.False(_gate.IsAuthenticated("Authentication-Results: attacker.test; spf=pass\r\nAuthentication-Results: mail.example.test; dmarc=pass\r\n", "sender@example.test"));
    }
}
