using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartLine.Application.Events;

namespace StartLine.Api.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    // POST /events  (Organizer only)
    [HttpPost]
    [Authorize(Roles = "Organizer")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var organizerId = GetCurrentUserId();
        var result = await _eventService.CreateEventAsync(request, organizerId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    // POST /events/{id}/races  (Organizer only)
    [HttpPost("{id:guid}/races")]
    [Authorize(Roles = "Organizer")]
    [ProducesResponseType(typeof(RaceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRace(Guid id, [FromBody] AddRaceRequest request, CancellationToken ct)
    {
        var organizerId = GetCurrentUserId();
        var result = await _eventService.AddRaceAsync(id, request, organizerId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    // GET /events  (public)
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EventSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _eventService.ListEventsAsync(page, pageSize, ct);
        return Ok(result);
    }

    // GET /events/{id}  (public)
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _eventService.GetEventAsync(id, ct);
        return Ok(result);
    }

    // PUT /events/{id}  (Organizer only)
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Organizer")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest request, CancellationToken ct)
    {
        var result = await _eventService.UpdateEventAsync(id, request, ct);
        return Ok(result);
    }

    // DELETE /events/{id}  (Organizer only – soft delete)
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Organizer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deletedBy = GetCurrentUserId() ?? Guid.Empty;
        await _eventService.SoftDeleteEventAsync(id, deletedBy, ct);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }
}
