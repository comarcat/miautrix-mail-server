using Miautrix.Mail.Protocols.Smtp;
using Xunit;

namespace Miautrix.Mail.ProtocolTests.Smtp;

[Trait("Category", "Dkim")]
public class DkimTests
{
    [Fact]
    public void When_outbound_message_leaves_domain_with_dkim_key_attaches_signature_that_verifies_against_public_key()
    {
        // ARRANGE
        var dkimService = new DkimService();
        var keyPair = dkimService.GenerateKeyPair(2048);

        var domain = "miautrix.local";
        var selector = "202609";

        var rawMessage =
            "From: alice@miautrix.local\r\n" +
            "To: bob@remote.org\r\n" +
            "Subject: Important Security Announcement\r\n" +
            "Date: Thu, 18 Sep 2026 12:00:00 +0000\r\n" +
            "\r\n" +
            "Hello Bob,\r\n" +
            "This is a signed message from Miautrix Mail Server.\r\n";

        // ACT: Sign outbound message
        var signedMessage = dkimService.SignMessage(rawMessage, domain, selector, keyPair.PrivateKeyPem);

        // ASSERT: Header attached
        Assert.StartsWith("DKIM-Signature:", signedMessage);
        Assert.Contains($"d={domain}", signedMessage);
        Assert.Contains($"s={selector}", signedMessage);

        // ACT: Verify against published public key
        bool isValid = dkimService.VerifyMessage(signedMessage, keyPair.PublicKeyBase64);

        // ASSERT: Must verify successfully
        Assert.True(isValid, "DKIM signature should verify successfully against the published public key");
    }

    [Fact]
    public void When_message_body_is_tampered_dkim_verification_fails()
    {
        // ARRANGE
        var dkimService = new DkimService();
        var keyPair = dkimService.GenerateKeyPair(2048);

        var rawMessage =
            "From: alice@miautrix.local\r\n" +
            "To: bob@remote.org\r\n" +
            "Subject: Authentic Notice\r\n" +
            "Date: Thu, 18 Sep 2026 12:00:00 +0000\r\n" +
            "\r\n" +
            "Original clean content.\r\n";

        var signedMessage = dkimService.SignMessage(rawMessage, "miautrix.local", "202609", keyPair.PrivateKeyPem);

        // ACT: Tamper with message body
        var tamperedMessage = signedMessage.Replace("Original clean content.", "Tampered malicious content.");
        bool isValid = dkimService.VerifyMessage(tamperedMessage, keyPair.PublicKeyBase64);

        // ASSERT: Verification must fail
        Assert.False(isValid, "Tampered message must fail DKIM verification");
    }

    [Fact]
    public void When_dns_auth_records_generated_they_conform_to_spec()
    {
        var dkimService = new DkimService();
        var keyPair = dkimService.GenerateKeyPair(2048);

        var dkimRecord = DnsAuthHelper.GenerateDkimDnsRecord(keyPair.PublicKeyBase64);
        var spfRecord = DnsAuthHelper.GenerateSpfRecord(new[] { "192.0.2.1", "198.51.100.1" }, new[] { "_spf.miautrix.com" });
        var dmarcRecord = DnsAuthHelper.GenerateDmarcRecord("dmarc-reports@miautrix.local", "quarantine");

        Assert.StartsWith("v=DKIM1; k=rsa; p=", dkimRecord);
        Assert.Contains("v=spf1 ip4:192.0.2.1 ip4:198.51.100.1 include:_spf.miautrix.com ~all", spfRecord);
        Assert.Equal("v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@miautrix.local; adkim=s; aspf=s", dmarcRecord);
    }
}
