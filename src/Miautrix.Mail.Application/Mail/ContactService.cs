using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Miautrix.Mail.Security;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Application.Mail;

public sealed class ContactService : IContactService
{
    private readonly AppDbContext _db;
    private readonly ITenantAuthorizationHelper _auth;

    public ContactService(AppDbContext db, ITenantAuthorizationHelper auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<IReadOnlyList<ContactDto>> ListContactsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var userEmail = await AssertUserContactAccessAsync(tenantId, userId, cancellationToken);

        var personal = await _db.Contacts
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => ToDto(c, "personal"))
            .ToListAsync(cancellationToken);

        var directoryDomain = GetDomainPart(userEmail);

        var directoryRows = await _db.Mailboxes
            .Where(m => m.TenantId == tenantId && m.IsActive && m.Kind == "user")
            .GroupJoin(
                _db.Users.Where(u => u.TenantId == tenantId && u.IsActive),
                m => m.Address.ToLower(),
                u => u.Email.ToLower(),
                (mailbox, users) => new { Mailbox = mailbox, User = users.FirstOrDefault() })
            .Where(row => row.User != null && !row.User.IsService)
            .OrderBy(row => row.Mailbox.Name)
            .ThenBy(row => row.Mailbox.Address)
            .Select(row => new
            {
                row.Mailbox.Id,
                row.Mailbox.Name,
                row.Mailbox.Address,
                row.Mailbox.Organization,
                row.Mailbox.Department,
                row.Mailbox.Phone,
                row.Mailbox.Kind,
                IsService = row.User!.IsService
            })
            .ToListAsync(cancellationToken);

        var directory = directoryRows
            .Where(m => string.Equals(GetDomainPart(m.Address), directoryDomain, StringComparison.OrdinalIgnoreCase))
            .Select(m => new ContactDto(
                m.Id,
                string.IsNullOrWhiteSpace(m.Name) ? m.Address : m.Name,
                m.Address,
                m.Organization,
                m.Department,
                m.Phone,
                "directory",
                m.Kind,
                false,
                m.IsService))
            .ToList();

        var groups = (await _db.Groups
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Address)
            .Select(g => new ContactDto(
                g.Id,
                string.IsNullOrWhiteSpace(g.Name) ? g.Address : g.Name,
                g.Address,
                null,
                null,
                null,
                "directory",
                "group",
                false,
                false))
            .ToListAsync(cancellationToken))
            .Where(c => string.Equals(GetDomainPart(c.Email), directoryDomain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return personal.Concat(directory).Concat(groups).ToList();
    }

    public async Task<ContactDto> CreateContactAsync(Guid tenantId, Guid userId, ContactRequest request, CancellationToken cancellationToken = default)
    {
        await AssertUserContactAccessAsync(tenantId, userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(contact, request, now);
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(contact, "personal");
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid tenantId, Guid userId, Guid contactId, ContactRequest request, CancellationToken cancellationToken = default)
    {
        await AssertUserContactAccessAsync(tenantId, userId, cancellationToken);
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == userId && c.Id == contactId, cancellationToken);
        if (contact is null)
        {
            return null;
        }

        Apply(contact, request, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(contact, "personal");
    }

    public async Task<ContactDto?> UpdateDirectoryContactAsync(Guid tenantId, Guid userId, Guid contactId, ContactRequest request, CancellationToken cancellationToken = default)
    {
        var userDomain = GetDomainPart(await AssertUserContactAccessAsync(tenantId, userId, cancellationToken));
        var canEditDirectory = await _db.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId && m.RoleId.HasValue)
            .Join(
                _db.Roles.Where(r => r.TenantId == tenantId && (r.Code == "owner" || r.Code == "admin")),
                m => m.RoleId!.Value,
                r => r.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);

        if (!canEditDirectory)
        {
            throw new ResourceNotFoundException();
        }

        var directoryDomain = userDomain;

        var mailbox = await _db.Mailboxes.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == contactId && m.IsActive, cancellationToken);
        if (mailbox is null || !string.Equals(GetDomainPart(mailbox.Address), directoryDomain, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        mailbox.Name = string.IsNullOrWhiteSpace(request.Name) ? mailbox.Address : request.Name.Trim();
        mailbox.Organization = string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim();
        mailbox.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        mailbox.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        mailbox.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var isService = await _db.Users
            .Where(u => u.TenantId == tenantId && u.Email.ToLower() == mailbox.Address.ToLower())
            .Select(u => u.IsService)
            .FirstOrDefaultAsync(cancellationToken);

        return new ContactDto(mailbox.Id, mailbox.Name, mailbox.Address, mailbox.Organization, mailbox.Department, mailbox.Phone, "directory", mailbox.Kind, true, isService);
    }

    public async Task<bool> DeleteContactAsync(Guid tenantId, Guid userId, Guid contactId, CancellationToken cancellationToken = default)
    {
        await AssertUserContactAccessAsync(tenantId, userId, cancellationToken);
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == userId && c.Id == contactId, cancellationToken);
        if (contact is null)
        {
            return false;
        }

        _db.Contacts.Remove(contact);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> AssertUserContactAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var userEmail = await _db.Users
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            throw new ResourceNotFoundException();
        }

        return userEmail;
    }

    private static string GetDomainPart(string email)
    {
        var at = email.LastIndexOf('@');
        return at >= 0 ? email[(at + 1)..].Trim().ToLowerInvariant() : string.Empty;
    }

    private static void Apply(Contact contact, ContactRequest request, DateTimeOffset now)
    {
        contact.Name = string.IsNullOrWhiteSpace(request.Name) ? request.Email.Trim() : request.Name.Trim();
        contact.Email = request.Email.Trim().ToLowerInvariant();
        contact.Organization = string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim();
        contact.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        contact.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        contact.UpdatedAt = now;
    }

    private static ContactDto ToDto(Contact contact, string book) => new(
        contact.Id,
        contact.Name,
        contact.Email,
        contact.Organization,
        contact.Department,
        contact.Phone,
        book);
}
