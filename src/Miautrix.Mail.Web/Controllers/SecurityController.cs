using Miautrix.Mail.Application.Admin;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SecurityController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public SecurityController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet("tenants")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> ListTenants(CancellationToken cancellationToken)
    {
        var tenants = await _adminService.ListTenantsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<TenantDto>>(tenants),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("security")]
    [ProducesResponseType(typeof(ApiResponse<SecuritySettingsDto>), StatusCodes.Status200OK)]
    public async Task<IResult> GetSecuritySettings(CancellationToken cancellationToken)
    {
        var domains = await _adminService.ListDomainsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);
        var domain = domains.FirstOrDefault();

        if (domain == null)
        {
            return Results.NotFound(new { error = new { code = "not_found", message = "No domains available for this tenant." } });
        }

        try
        {
            var settings = await _adminService.GetSecuritySettingsAsync(
                domain.Id,
                _context.CurrentUserId,
                cancellationToken);

            return Results.Json(
                new ApiResponse<SecuritySettingsDto>(settings),
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Domain not found"))
        {
            return Results.NotFound(new { error = new { code = "not_found", message = ex.Message } });
        }
    }

    [HttpPatch("security")]
    [ProducesResponseType(typeof(ApiResponse<SecuritySettingsDto>), StatusCodes.Status200OK)]
    public async Task<IResult> UpdateSecuritySettings(
        [FromBody] UpdateSecuritySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var domains = await _adminService.ListDomainsAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);
        var domain = domains.FirstOrDefault();

        if (domain == null)
        {
            return Results.NotFound(new { error = new { code = "not_found", message = "No domains available for this tenant." } });
        }

        try
        {
            var settings = await _adminService.UpdateSecuritySettingsAsync(
                domain.Id,
                _context.CurrentUserId,
                request,
                cancellationToken);

            return Results.Json(
                new ApiResponse<SecuritySettingsDto>(settings),
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Domain not found"))
        {
            return Results.NotFound(new { error = new { code = "not_found", message = ex.Message } });
        }
    }

    [HttpGet("domains/{domainId:guid}/security")]
    [ProducesResponseType(typeof(ApiResponse<SecuritySettingsDto>), StatusCodes.Status200OK)]
    public async Task<IResult> GetDomainSecuritySettings(Guid domainId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _adminService.GetSecuritySettingsAsync(
                domainId,
                _context.CurrentUserId,
                cancellationToken);

            return Results.Json(
                new ApiResponse<SecuritySettingsDto>(settings),
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Domain not found"))
        {
            return Results.NotFound(new { error = new { code = "not_found", message = ex.Message } });
        }
    }

    [HttpPatch("domains/{domainId:guid}/security")]
    [ProducesResponseType(typeof(ApiResponse<SecuritySettingsDto>), StatusCodes.Status200OK)]
    public async Task<IResult> UpdateDomainSecuritySettings(
        Guid domainId,
        [FromBody] UpdateSecuritySettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _adminService.UpdateSecuritySettingsAsync(
                domainId,
                _context.CurrentUserId,
                request,
                cancellationToken);

            return Results.Json(
                new ApiResponse<SecuritySettingsDto>(settings),
                ApiJson.Options,
                statusCode: StatusCodes.Status200OK);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Domain not found"))
        {
            return Results.NotFound(new { error = new { code = "not_found", message = ex.Message } });
        }
    }
}
