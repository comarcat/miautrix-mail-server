using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Miautrix.Mail.Application.Admin;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/domains")]
public sealed class DomainController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public DomainController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DomainDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var domains = await _adminService.ListDomainsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<DomainDto>>(domains),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{domainId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid domainId, CancellationToken cancellationToken)
    {
        var domain = await _adminService.GetDomainAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            domainId,
            cancellationToken);

        if (domain is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "domain_not_found",
                "Domain not found.");
        }

        return Results.Json(
            new ApiResponse<DomainDto>(domain),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DomainDto>), StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromBody] CreateDomainRequest request, CancellationToken cancellationToken)
    {
        var domain = await _adminService.CreateDomainAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            request,
            cancellationToken);

        return Results.Json(
            new ApiResponse<DomainDto>(domain),
            ApiJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    [HttpPost("{domainId:guid}/verify")]
    [ProducesResponseType(typeof(ApiResponse<VerifyDomainResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Verify(Guid domainId, CancellationToken cancellationToken)
    {
        var result = await _adminService.VerifyDomainAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            domainId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<VerifyDomainResult>(result),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{domainId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid domainId, CancellationToken cancellationToken)
    {
        var deleted = await _adminService.DeleteDomainAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            domainId,
            cancellationToken);

        if (!deleted)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "domain_not_found",
                "Domain not found.");
        }

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }

    [HttpPut("{domainId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid domainId, [FromBody] UpdateDomainRequest request, CancellationToken cancellationToken)
    {
        var updated = await _adminService.UpdateDomainAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            domainId,
            request,
            cancellationToken);

        if (updated is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "domain_not_found",
                "Domain not found.");
        }

        return Results.Json(
            new ApiResponse<DomainDto>(updated),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}
