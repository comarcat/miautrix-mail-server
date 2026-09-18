using Miautrix.Mail.Domain;
using Miautrix.Mail.Protocols.Smtp;
using Miautrix.Mail.Queue;
using Xunit;

namespace Miautrix.Mail.ProtocolTests.Smtp;

[Trait("Category", "Smtp")]
public class SmtpProtocolTests
{
    private class MockSmtpQueueManager : ISmtpQueueManager
    {
        public List<SmtpQueueItem> EnqueuedItems { get; } = new();

        public Task<SmtpQueueItem> EnqueueAsync(
            Guid tenantId,
            string sender,
            string recipient,
            string rawMessage,
            string? subject = null,
            CancellationToken cancellationToken = default)
        {
            var item = new SmtpQueueItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sender = sender,
                Recipient = recipient,
                Subject = subject,
                RawMessage = rawMessage,
                Status = "Pending",
                Attempts = 0,
                NextAttemptAt = DateTimeOffset.UtcNow
            };
            EnqueuedItems.Add(item);
            return Task.FromResult(item);
        }

        public Task<SmtpDeliveryAttempt> RecordDeliveryAttemptAsync(
            Guid queueItemId,
            bool success,
            string? errorMessage = null,
            int? responseCode = null,
            CancellationToken cancellationToken = default)
        {
            var attempt = new SmtpDeliveryAttempt
            {
                Id = Guid.NewGuid(),
                QueueItemId = queueItemId,
                Success = success,
                ErrorMessage = errorMessage,
                ResponseCode = responseCode
            };
            return Task.FromResult(attempt);
        }
    }

    private class MockDomainValidator : ISmtpDomainValidator
    {
        private readonly HashSet<string> _localDomains;
        private readonly Guid _tenantId;

        public MockDomainValidator(Guid tenantId, IEnumerable<string> localDomains)
        {
            _tenantId = tenantId;
            _localDomains = new HashSet<string>(localDomains, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsDomainLocal(string emailAddress, out Guid tenantId)
        {
            tenantId = Guid.Empty;
            int atIdx = emailAddress.LastIndexOf('@');
            if (atIdx < 0 || atIdx == emailAddress.Length - 1)
            {
                return false;
            }

            var domain = emailAddress[(atIdx + 1)..];
            if (_localDomains.Contains(domain))
            {
                tenantId = _tenantId;
                return true;
            }

            return false;
        }
    }

    private class MockAuthenticator : ISmtpAuthenticator
    {
        private readonly Guid _tenantId;
        private readonly Guid _userId;

        public MockAuthenticator(Guid tenantId, Guid userId)
        {
            _tenantId = tenantId;
            _userId = userId;
        }

        public bool Authenticate(string username, string password, bool isTlsEncrypted, out Guid tenantId, out Guid userId)
        {
            tenantId = _tenantId;
            userId = _userId;
            return isTlsEncrypted && username == "user@local.org" && password == "ValidPassword123!";
        }
    }

    [Fact]
    public async Task When_message_is_accepted_for_local_recipient_the_system_shall_enqueue_exactly_one_row()
    {
        // ARRANGE
        var tenantId = Guid.NewGuid();
        var domainValidator = new MockDomainValidator(tenantId, new[] { "local.org", "tenant.com" });
        var queueManager = new MockSmtpQueueManager();
        var inboundHandler = new SmtpInboundHandler(domainValidator, queueManager);

        var recipient = "alice@local.org";
        var sender = "bob@remote.net";
        var rawMessage = "From: bob@remote.net\r\nTo: alice@local.org\r\nSubject: Hello\r\n\r\nTest body";

        // ACT
        var rcptResponse = inboundHandler.HandleRcptTo(recipient, out var resolvedTenantId);
        Assert.True(rcptResponse.IsSuccess);
        Assert.Equal(tenantId, resolvedTenantId);

        var (dataResponse, queueItem) = await inboundHandler.HandleDataAsync(resolvedTenantId, sender, recipient, rawMessage, "Hello");

        // ASSERT: Exactly one row enqueued in smtp_queue
        Assert.True(dataResponse.IsSuccess);
        Assert.Equal(250, dataResponse.Code);
        Assert.NotNull(queueItem);
        Assert.Single(queueManager.EnqueuedItems);
        Assert.Equal(queueItem.Id, queueManager.EnqueuedItems[0].Id);
        Assert.Equal("alice@local.org", queueManager.EnqueuedItems[0].Recipient);
    }

    [Fact]
    public async Task When_message_arrives_for_recipient_outside_tenant_domains_rejects_with_550_571_and_shall_not_enqueue()
    {
        // ARRANGE: Inbound recipient outside all tenant domains (open relay attempt)
        var tenantId = Guid.NewGuid();
        var domainValidator = new MockDomainValidator(tenantId, new[] { "local.org" });
        var queueManager = new MockSmtpQueueManager();
        var inboundHandler = new SmtpInboundHandler(domainValidator, queueManager);

        var foreignRecipient = "victim@external-domain.com";
        var sender = "spammer@bad.org";

        // ACT: RCPT TO
        var rcptResponse = inboundHandler.HandleRcptTo(foreignRecipient, out var resolvedTenantId);

        // ASSERT: Must be rejected with 550 5.7.1 Relay access denied
        Assert.False(rcptResponse.IsSuccess);
        Assert.Equal(550, rcptResponse.Code);
        Assert.Equal("5.7.1", rcptResponse.EnhancedCode);
        Assert.Contains("Relay access denied", rcptResponse.Message);

        // ACT: Attempt DATA despite rejection
        var (dataResponse, queueItem) = await inboundHandler.HandleDataAsync(
            tenantId,
            sender,
            foreignRecipient,
            "Subject: spam",
            "spam");

        // ASSERT: Must not enqueue
        Assert.False(dataResponse.IsSuccess);
        Assert.Null(queueItem);
        Assert.Empty(queueManager.EnqueuedItems);
    }

    [Fact]
    public async Task When_unauthenticated_client_attempts_submission_on_587_rejects_with_530_570()
    {
        // ARRANGE
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auth = new MockAuthenticator(tenantId, userId);
        var queueManager = new MockSmtpQueueManager();
        var submissionHandler = new SmtpSubmissionHandler(auth, queueManager);

        // ACT: Unauthenticated client attempts submission on 587
        var (response, queueItem) = await submissionHandler.HandleSubmissionAsync(
            isAuthenticated: false,
            isTlsEncrypted: true,
            tenantId: null,
            sender: "user@local.org",
            recipient: "recipient@remote.com",
            rawMessage: "Test payload");

        // ASSERT: Must reject with 530 5.7.0 Authentication required
        Assert.False(response.IsSuccess);
        Assert.Equal(530, response.Code);
        Assert.Equal("5.7.0", response.EnhancedCode);
        Assert.Contains("Authentication required", response.Message);
        Assert.Null(queueItem);
        Assert.Empty(queueManager.EnqueuedItems);
    }

    [Fact]
    public void When_client_attempts_auth_without_encryption_rejects()
    {
        // ARRANGE
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auth = new MockAuthenticator(tenantId, userId);
        var queueManager = new MockSmtpQueueManager();
        var submissionHandler = new SmtpSubmissionHandler(auth, queueManager);

        // ACT: Client attempts AUTH over unencrypted connection
        var authResponse = submissionHandler.HandleAuth(
            username: "user@local.org",
            password: "ValidPassword123!",
            isTlsEncrypted: false,
            out var resolvedTenantId,
            out var resolvedUserId);

        // ASSERT: Refuses authentication without encryption
        Assert.False(authResponse.IsSuccess);
        Assert.Null(resolvedTenantId);
        Assert.Null(resolvedUserId);
        Assert.Equal(538, authResponse.Code);
    }

    [Fact]
    public void When_delivery_fails_retry_delays_are_strictly_increasing()
    {
        // ARRANGE
        var retryPolicy = new ExponentialBackoffWithJitterRetryPolicy(
            baseDelay: TimeSpan.FromSeconds(5),
            maxDelay: TimeSpan.FromHours(24),
            jitterRatio: 0.25);

        // ACT: Calculate delays for sequential attempts 1 through 10
        var delays = new List<double>();
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            var delay = retryPolicy.GetNextRetryDelay(attempt);
            delays.Add(delay.TotalSeconds);
        }

        // ASSERT: Every delay is strictly greater than the previous one
        for (int i = 1; i < delays.Count; i++)
        {
            Assert.True(
                delays[i] > delays[i - 1],
                $"Attempt {i + 1} delay ({delays[i]}s) was not strictly greater than attempt {i} delay ({delays[i - 1]}s)");
        }
    }
}
