using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly IContactService _contacts;
    private readonly IRequestContextAccessor _context;

    public ContactsController(IContactService contacts, IRequestContextAccessor context)
    {
        _contacts = contacts;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ContactDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken cancellationToken = default)
    {
        var contacts = await _contacts.ListContactsAsync(_context.CurrentTenantId, _context.CurrentUserId, cancellationToken);
        return Results.Json(new ApiResponse<IReadOnlyList<ContactDto>>(contacts), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromBody] ContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await _contacts.CreateContactAsync(_context.CurrentTenantId, _context.CurrentUserId, request, cancellationToken);
        return Results.Json(new ApiResponse<ContactDto>(contact), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{contactId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid contactId, [FromBody] ContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await _contacts.UpdateContactAsync(_context.CurrentTenantId, _context.CurrentUserId, contactId, request, cancellationToken);
        return contact is null
            ? Results.NotFound()
            : Results.Json(new ApiResponse<ContactDto>(contact), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPut("directory/{contactId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateDirectory(Guid contactId, [FromBody] ContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await _contacts.UpdateDirectoryContactAsync(_context.CurrentTenantId, _context.CurrentUserId, contactId, request, cancellationToken);
        return contact is null
            ? Results.NotFound()
            : Results.Json(new ApiResponse<ContactDto>(contact), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{contactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid contactId, CancellationToken cancellationToken = default)
    {
        var deleted = await _contacts.DeleteContactAsync(_context.CurrentTenantId, _context.CurrentUserId, contactId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
