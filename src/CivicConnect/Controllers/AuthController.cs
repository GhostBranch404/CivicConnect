using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;

namespace CivicConnect.Core.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserRegistrationService _registration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserRegistrationService registration, ILogger<AuthController> logger)
    {
        _registration = registration;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting("signup")]
    public async Task<IActionResult> Register(
        [FromBody] UserRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = await _registration.RegisterAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new
            {
                UserId = userId,
                Message = "Account created. Enter the confirmation code sent to your selected contact method."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { Message = "An account with that email address or phone number already exists." });
        }
        catch (ConfirmationDeliveryException ex)
        {
            _logger.LogError(
                "Confirmation delivery failed during registration ({ExceptionType}).",
                ex.InnerException?.GetType().Name ?? ex.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "The account was created, but the confirmation message could not be delivered. Request a new code."
            });
        }
    }

    [HttpPost("confirm")]
    [EnableRateLimiting("confirmation")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmUserRequest request)
    {
        try
        {
            if (!await _registration.ConfirmAsync(request))
                return BadRequest(new { Message = "The confirmation code is invalid or expired." });

            return Ok(new { Message = "The account was confirmed successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("confirmation")]
    public async Task<IActionResult> ResendConfirmation(
        [FromBody] ResendConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _registration.ResendConfirmationAsync(request, cancellationToken);
            return Ok(new
            {
                Message = "If an unconfirmed account exists for that contact, a new code has been sent."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (ConfirmationDeliveryException ex)
        {
            _logger.LogError(
                "Confirmation delivery failed during resend ({ExceptionType}).",
                ex.InnerException?.GetType().Name ?? ex.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "The confirmation message could not be delivered. Try again later."
            });
        }
    }
}
