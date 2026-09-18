using Miautrix.Mail.Domain;
using Miautrix.Mail.Identity;
using Miautrix.Mail.Security;
using Xunit;

namespace Miautrix.Mail.SecurityTests.Isolation;

[Trait("Category", "Isolation")]
public class IsolationTests
{
    private class MockPermissionRepo : IPermissionRepository
    {
        public bool HasPermission(Guid tenantId, Guid userId, string permissionCode)
        {
            return permissionCode != "forbidden.action";
        }

        public int GetTenantOwnerCount(Guid tenantId) => 1;

        public bool IsUserTenantOwner(Guid tenantId, Guid userId) => true;
    }

    [Fact]
    public void When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_B_returns_404_and_does_not_leak_existence()
    {
        // ARRANGE
        var eventSink = new InMemorySecurityEventSink();
        var repo = new MockPermissionRepo();
        var authHelper = new TenantAuthorizationHelper(repo, eventSink);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userInTenantA = Guid.NewGuid();

        var mailboxTenantB = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB
        };

        // ACT & ASSERT: Must throw ResourceNotFoundException (translates to 404 in API surface)
        var exception = Assert.Throws<ResourceNotFoundException>(() =>
        {
            authHelper.AuthorizeAccess(tenantA, userInTenantA, mailboxTenantB, "mailbox.read");
        });

        Assert.Equal("Resource not found.", exception.Message);

        // Verify security event logged for cross-tenant breach attempt
        var events = eventSink.GetEvents();
        Assert.Single(events);
        Assert.Equal(SecurityEventCodes.AuthPrivilegeEscalation, events[0].EventCode);
    }

    [Fact]
    public void When_user_in_tenant_A_requests_mailbox_belonging_to_tenant_A_succeeds()
    {
        // ARRANGE
        var eventSink = new InMemorySecurityEventSink();
        var repo = new MockPermissionRepo();
        var authHelper = new TenantAuthorizationHelper(repo, eventSink);

        var tenantA = Guid.NewGuid();
        var userInTenantA = Guid.NewGuid();

        var mailboxTenantA = new Mailbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA
        };

        // ACT
        var authorizedMailbox = authHelper.AuthorizeAccess(tenantA, userInTenantA, mailboxTenantA, "mailbox.read");

        // ASSERT
        Assert.NotNull(authorizedMailbox);
        Assert.Equal(mailboxTenantA.Id, authorizedMailbox.Id);
    }

    [Fact]
    public void When_attempting_to_demote_last_tenant_owner_throws_exception()
    {
        // ARRANGE
        var eventSink = new InMemorySecurityEventSink();
        var repo = new MockPermissionRepo(); // configured with ownerCount = 1 and isOwner = true
        var authHelper = new TenantAuthorizationHelper(repo, eventSink);

        var tenantId = Guid.NewGuid();
        var lastOwnerUserId = Guid.NewGuid();

        // ACT & ASSERT: Cannot demote the last owner
        Assert.Throws<LastOwnerDemotionException>(() =>
        {
            authHelper.ValidateOwnerDemotion(tenantId, lastOwnerUserId);
        });

        var events = eventSink.GetEvents();
        Assert.Single(events);
        Assert.Equal(SecurityEventCodes.AuthPrivilegeEscalation, events[0].EventCode);
    }
}
