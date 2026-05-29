using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartLine.Application.Registrations;
using StartLine.Domain.Registrations;

namespace StartLine.Api.Controllers;

[ApiController]
[Route("registrations")]
public class RegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationsController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    // POST /registrations  (Athlete only)
    [HttpPost]
    [Authorize(Roles = "Athlete")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] CreateRegistrationRequest request, CancellationToken ct)
    {
        var athleteId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Athlete ID could not be determined.");

        var result = await _registrationService.RegisterAsync(request, athleteId, ct);

        if (result.Status == RegistrationStatus.Waitlisted.ToString())
            return StatusCode(StatusCodes.Status202Accepted, result);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    // GET /registrations/{id}  (owning Athlete only)
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Athlete")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var athleteId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Athlete ID could not be determined.");

        var result = await _registrationService.GetRegistrationAsync(id, athleteId, ct);
        return Ok(result);
    }

    // POST /registrations/{id}/pay  (owning Athlete only)
    [HttpPost("{id:guid}/pay")]
    [Authorize(Roles = "Athlete")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(Guid id, CancellationToken ct)
    {
        var athleteId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Athlete ID could not be determined.");

        var result = await _registrationService.PayRegistrationAsync(id, athleteId, ct);
        return Ok(result);
    }

    // POST /registrations/{id}/cancel  (owning Athlete only)
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Athlete")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var athleteId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Athlete ID could not be determined.");

        var result = await _registrationService.CancelRegistrationAsync(id, athleteId, ct);
        return Ok(result);
    }

    // DELETE /registrations/{id}  (owning Athlete only)
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Athlete")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var athleteId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Athlete ID could not be determined.");

        var result = await _registrationService.CancelRegistrationAsync(id, athleteId, ct);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }
}
