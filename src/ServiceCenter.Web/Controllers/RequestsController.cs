using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceCenter.Web.Models;
using ServiceCenter.Web.Services;

namespace ServiceCenter.Web.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class RequestsController : ControllerBase
{
    private readonly RequestService _service;

    public RequestsController(RequestService service)
    {
        _service = service;
    }

    private (long UserId, string Role) CurrentUser()
    {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        return (id, role);
    }

    [HttpGet("requests")]
    public ActionResult<PagedResult<ServiceRequestSummaryDto>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.GetRequests(uid, role, status, search, sortBy, page, pageSize));
    }

    [HttpGet("requests/{id:long}")]
    public ActionResult<ServiceRequestDetailsDto> GetById(long id)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.GetRequestDetails(id, uid, role));
    }

    [HttpPost("requests")]
    public ActionResult<ServiceRequestDetailsDto> Create([FromBody] CreateServiceRequestDto dto)
    {
        var (uid, role) = CurrentUser();
        var created = _service.CreateRequest(dto, uid, role);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("requests/{id:long}/assign")]
    public ActionResult<ServiceRequestDetailsDto> Assign(long id, [FromBody] AssignTechnicianDto dto)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.AssignTechnician(id, dto, uid, role));
    }

    [HttpPut("requests/{id:long}/diagnostics")]
    public ActionResult<ServiceRequestDetailsDto> UpdateDiagnostics(long id, [FromBody] UpdateDiagnosticsDto dto)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.UpdateDiagnostics(id, dto, uid, role));
    }

    [HttpPost("requests/{id:long}/works")]
    public ActionResult<ServiceRequestDetailsDto> AddWork(long id, [FromBody] AddWorkItemDto dto)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.AddWorkItem(id, dto, uid, role));
    }

    [HttpDelete("requests/{id:long}/works/{workId:long}")]
    public ActionResult<ServiceRequestDetailsDto> RemoveWork(long id, long workId, [FromQuery] int version)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.RemoveWorkItem(id, workId, version, uid, role));
    }

    [HttpPost("requests/{id:long}/parts")]
    public ActionResult<ServiceRequestDetailsDto> AddPart(long id, [FromBody] AddRequestPartDto dto)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.AddRequestPart(id, dto, uid, role));
    }

    [HttpDelete("requests/{id:long}/parts/{requestPartId:long}")]
    public ActionResult<ServiceRequestDetailsDto> RemovePart(long id, long requestPartId, [FromQuery] int version)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.RemoveRequestPart(id, requestPartId, version, uid, role));
    }

    [HttpPost("requests/{id:long}/status")]
    public ActionResult<ServiceRequestDetailsDto> ChangeStatus(long id, [FromBody] ChangeStatusDto dto)
    {
        var (uid, role) = CurrentUser();
        return Ok(_service.ChangeStatus(id, dto, uid, role));
    }

    [HttpGet("parts")]
    public ActionResult<IReadOnlyList<PartRecord>> ListParts()
    {
        return Ok(_service.GetParts());
    }
}
