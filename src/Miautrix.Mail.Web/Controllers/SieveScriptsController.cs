using Miautrix.Mail.Application.Mail;
using Miautrix.Mail.Protocols.Sieve;
using Miautrix.Mail.Web.Contracts;
using Miautrix.Mail.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Miautrix.Mail.Web.Controllers;

[ApiController]
[Route("api/v1/mail/sieve/scripts")]
public sealed class SieveScriptsController : ControllerBase
{
    private readonly ISieveScriptApiService _service;
    private readonly IRequestContextAccessor _context;

    public SieveScriptsController(ISieveScriptApiService service, IRequestContextAccessor context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SieveScriptDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> List([FromQuery] Guid mailboxId, CancellationToken ct) =>
        Results.Json(new ApiResponse<IReadOnlyList<SieveScriptDto>>(
            await _service.ListAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, ct)), ApiJson.Options);

    [HttpGet("{scriptId:guid}")]
    public async Task<IResult> Get(Guid scriptId, [FromQuery] Guid mailboxId, CancellationToken ct)
    {
        var script = await _service.GetAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, scriptId, ct);
        return script is null ? Results.NotFound() : Results.Json(new ApiResponse<SieveScriptDto>(script), ApiJson.Options);
    }

    [HttpPost]
    public async Task<IResult> Create([FromQuery] Guid mailboxId, [FromBody] SieveScriptSaveRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, request, ct);
        return result.Success
            ? Results.Json(new ApiResponse<SieveScriptDto>(result.Script!), ApiJson.Options, statusCode: StatusCodes.Status201Created)
            : CompileError(result.Diagnostic);
    }

    [HttpPut("{scriptId:guid}")]
    public async Task<IResult> Update(Guid scriptId, [FromQuery] Guid mailboxId, [FromBody] SieveScriptSaveRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, scriptId, request, ct);
        if (result is null) return Results.NotFound();
        return result.Success ? Results.Json(new ApiResponse<SieveScriptDto>(result.Script!), ApiJson.Options) : CompileError(result.Diagnostic);
    }

    [HttpPost("{scriptId:guid}/activate")]
    public async Task<IResult> Activate(Guid scriptId, [FromQuery] Guid mailboxId, CancellationToken ct) =>
        await SetActive(scriptId, mailboxId, true, ct);

    [HttpPost("{scriptId:guid}/deactivate")]
    public async Task<IResult> Deactivate(Guid scriptId, [FromQuery] Guid mailboxId, CancellationToken ct) =>
        await SetActive(scriptId, mailboxId, false, ct);

    [HttpDelete("{scriptId:guid}")]
    public async Task<IResult> Delete(Guid scriptId, [FromQuery] Guid mailboxId, CancellationToken ct) =>
        await _service.DeleteAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, scriptId, ct)
            ? Results.NoContent()
            : Results.NotFound();

    private async Task<IResult> SetActive(Guid scriptId, Guid mailboxId, bool active, CancellationToken ct) =>
        await _service.SetActiveAsync(_context.CurrentTenantId, _context.CurrentUserId, mailboxId, scriptId, active, ct)
            ? Results.NoContent()
            : Results.NotFound();

    private static IResult CompileError(SieveParseResult diagnostic)
    {
        var details = new Dictionary<string, string[]>();
        if (diagnostic.ErrorLine is int line) details["line"] = [line.ToString()];
        if (diagnostic.ErrorColumn is int column) details["column"] = [column.ToString()];
        return Results.UnprocessableEntity(new ApiError("sieve_compile_error", diagnostic.ErrorMessage ?? "Sieve compilation failed.", details));
    }
}
