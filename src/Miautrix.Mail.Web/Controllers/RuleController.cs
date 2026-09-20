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
[Route("api/v1/mail/rules")]
public sealed class RuleController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRequestContextAccessor _context;

    public RuleController(IAdminService adminService, IRequestContextAccessor context)
    {
        _adminService = adminService;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MailFlowRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List(CancellationToken cancellationToken)
    {
        var rules = await _adminService.ListRulesAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            cancellationToken);

        return Results.Json(
            new ApiResponse<IReadOnlyList<MailFlowRuleDto>>(rules),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MailFlowRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var rule = await _adminService.GetRuleAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            id,
            cancellationToken);

        if (rule is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "rule_not_found",
                "Mail flow rule not found.");
        }

        return Results.Json(
            new ApiResponse<MailFlowRuleDto>(rule),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MailFlowRuleDto>), StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromBody] CreateRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _adminService.CreateRuleAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            request,
            cancellationToken);

        return Results.Json(
            new ApiResponse<MailFlowRuleDto>(rule),
            ApiJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MailFlowRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid id, [FromBody] UpdateRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _adminService.UpdateRuleAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            id,
            request,
            cancellationToken);

        if (rule is null)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "rule_not_found",
                "Mail flow rule not found.");
        }

        return Results.Json(
            new ApiResponse<MailFlowRuleDto>(rule),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _adminService.DeleteRuleAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            id,
            cancellationToken);

        if (!deleted)
        {
            return ApiResults.Error(
                HttpContext,
                StatusCodes.Status404NotFound,
                "rule_not_found",
                "Mail flow rule not found.");
        }

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }

    [HttpPost("simulate")]
    [ProducesResponseType(typeof(ApiResponse<RuleSimulationResult>), StatusCodes.Status200OK)]
    public async Task<IResult> Simulate([FromBody] RuleSimulationRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.SimulateRuleAsync(
            _context.CurrentTenantId,
            _context.CurrentUserId,
            request,
            cancellationToken);

        return Results.Json(
            new ApiResponse<RuleSimulationResult>(result),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }
}
