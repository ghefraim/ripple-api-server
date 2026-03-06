using Application.Features.Audit.GetAuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class AuditController : ApiControllerBase
{
    /// <summary>
    /// Get audit logs with optional filtering
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] GetAuditLogsQuery request)
    {
        return Ok(await Mediator.Send(request));
    }
}