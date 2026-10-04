namespace Miautrix.Mail.Application.Mail;

public interface IContactService
{
    Task<IReadOnlyList<ContactDto>> ListContactsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<ContactDto> CreateContactAsync(Guid tenantId, Guid userId, ContactRequest request, CancellationToken cancellationToken = default);

    Task<ContactDto?> UpdateContactAsync(Guid tenantId, Guid userId, Guid contactId, ContactRequest request, CancellationToken cancellationToken = default);

    Task<ContactDto?> UpdateDirectoryContactAsync(Guid tenantId, Guid userId, Guid contactId, ContactRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteContactAsync(Guid tenantId, Guid userId, Guid contactId, CancellationToken cancellationToken = default);
}
