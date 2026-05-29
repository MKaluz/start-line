using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StartLine.Application.Auth;
using StartLine.Application.Events;
using StartLine.Application.Registrations;
using System.Diagnostics;

namespace StartLine.Api;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        ProblemDetails problemDetails;

        switch (exception)
        {
            case DuplicateEmailException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                break;

            case InvalidCredentialsException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                break;

            case AccountLockedException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                break;

            case InvalidRefreshTokenException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                break;

            case EventNotFoundException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                break;

            case RaceNotFoundException:
            case RegistrationNotFoundException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                break;

            case CapacityExceededException:
            case RegistrationInvalidStatusException:
            case ReservationExpiredException:
            case RegistrationCannotBeCancelledException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                break;

            case AgeValidationException:
            case GenderValidationException:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Validation Error",
                    Detail = exception.Message,
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                break;

            default:
                _logger.LogError(exception, "Unhandled exception occurred");
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred",
                    Type = "https://tools.ietf.org/html/rfc7807"
                };
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        problemDetails.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
