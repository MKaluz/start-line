using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartLine.Application.Registrations;

namespace StartLine.Api.Controllers;

[ApiController]
[Route("organizer/registrations")]
[Authorize(Roles = "Organizer")]
public class OrganizerRegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public OrganizerRegistrationsController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    // PATCH /organizer/registrations/{id}/status  (Organizer only)
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        [FromBody] SetRegistrationStatusRequest request,
        CancellationToken ct)
    {
        if (!string.Equals(request.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = $"Status '{request.Status}' is not supported. Only 'Cancelled' is accepted.",
                Type = "https://tools.ietf.org/html/rfc7807"
            });

        var result = await _registrationService.ForceCancelRegistrationAsync(id, ct);
        return Ok(result);
    }
}
