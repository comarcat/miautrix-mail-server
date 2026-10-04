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
[Route("api/v1/mail/rules")]
public sealed class SieveRuleController : ControllerBase
{
    private readonly ISieveRuleService _rules;
    private readonly IRequestContextAccessor _context;

    public SieveRuleController(ISieveRuleService rules, IRequestContextAccessor context)
    {
        _rules = rules;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SieveFilterRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List([FromQuery] Guid mailboxId, CancellationToken cancellationToken = default)
    {
        var rules = await _rules.ListRulesAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, cancellationToken);
        return Results.Json(new ApiResponse<IReadOnlyList<SieveFilterRuleDto>>(rules), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SieveFilterRuleDto>), StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromQuery] Guid mailboxId, [FromBody] SieveFilterRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.CreateRuleAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, request, cancellationToken);
        return Results.Json(new ApiResponse<SieveFilterRuleDto>(rule), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{ruleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SieveFilterRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(Guid ruleId, [FromQuery] Guid mailboxId, [FromBody] SieveFilterRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await _rules.UpdateRuleAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, ruleId, request, cancellationToken);
        return rule is null
            ? Results.NotFound()
            : Results.Json(new ApiResponse<SieveFilterRuleDto>(rule), ApiJson.Options, statusCode: StatusCodes.Status200OK);
    }

    [HttpPut("{ruleId:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> SetActive(Guid ruleId, [FromQuery] Guid mailboxId, [FromBody] SieveRuleActiveRequest request, CancellationToken cancellationToken = default)
    {
        var updated = await _rules.SetRuleActiveAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, ruleId, request.Active, cancellationToken);
        return updated ? Results.NoContent() : Results.NotFound();
    }

    [HttpDelete("{ruleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(Guid ruleId, [FromQuery] Guid mailboxId, CancellationToken cancellationToken = default)
    {
        var deleted = await _rules.DeleteRuleAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, ruleId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
