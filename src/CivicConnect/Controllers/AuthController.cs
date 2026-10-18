using CivicConnect.Core.Data;
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
    private const string MigrationHint =
        "User accounts are not set up in the database yet. Run the CivicConnect database migrations.";
    private const string AlreadyExists =
        "An account with that email address already exists. Try logging in instead.";

    private readonly UserRegistrationService _registration;
    private readonly IUserService _users;
    private readonly IDeveloperLogRepository _devLogs;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserRegistrationService registration,
        IUserService users,
        IDeveloperLogRepository devLogs,
        ILogger<AuthController> logger)
    {
        _registration = registration;
        _users = users;
        _devLogs = devLogs;
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
                Message = "We've emailed you a 6-digit code. Enter it to finish creating your account."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (AccountAlreadyExistsException)
        {
            return Conflict(new { Message = AlreadyExists });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { Message = AlreadyExists });
        }
        catch (MySqlException ex) when (ex.Number == 1054)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = MigrationHint });
        }
        catch (ConfirmationDeliveryException ex)
        {
            await LogDeliveryFailureAsync("register", ex);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "We couldn't send the confirmation email, so your account wasn't created. " +
                          "Check the email address and try again in a few minutes."
            });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _users.LoginAsync(request);
            if (user is null)
                return Unauthorized(new { Message = "Incorrect email or password, or the account has not been confirmed yet." });

            return Ok(user);
        }
        catch (MySqlException ex) when (ex.Number == 1054)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = MigrationHint });
        }
    }

    [HttpPost("confirm")]
    [EnableRateLimiting("confirmation")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmUserRequest request)
    {
        try
        {
            if (!await _registration.ConfirmAsync(request))
                return BadRequest(new { Message = "That code is wrong or has expired. Check the latest email, or request a new code." });

            return Ok(new { Message = "Your account is confirmed. You can now log in." });
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
            return Ok(new { Message = "If that email has an unconfirmed account, a new code has been sent." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (ConfirmationDeliveryException ex)
        {
            await LogDeliveryFailureAsync("resend", ex);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "We couldn't send the confirmation email. Try again in a few minutes."
            });
        }
    }

    public sealed record ForgotPasswordBody(string? Email, string? Code, string? Token, string? NewPassword);

    [HttpPost("forgot-password")]
    [EnableRateLimiting("confirmation")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordBody body,
        [FromServices] PasswordResetService reset,
        CancellationToken cancellationToken)
    {
        try
        {
            await reset.RequestCodeAsync(body.Email, cancellationToken);
            return Ok(new { Message = "If that email has an account, a reset code has been sent." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Password reset email failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = "We couldn't send the reset email. Try again in a few minutes."
            });
        }
    }

    [HttpPost("verify-reset-code")]
    [EnableRateLimiting("confirmation")]
    public async Task<IActionResult> VerifyResetCode(
        [FromBody] ForgotPasswordBody body,
        [FromServices] PasswordResetService reset)
    {
        try
        {
            var token = await reset.VerifyCodeAsync(body.Email, body.Code);
            if (token is null)
                return BadRequest(new { Message = "That code is wrong or has expired. Request a new code." });
            return Ok(new { Token = token });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("confirmation")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ForgotPasswordBody body,
        [FromServices] PasswordResetService reset)
    {
        try
        {
            if (!await reset.ResetPasswordAsync(body.Token, body.NewPassword))
                return BadRequest(new { Message = "Your reset session has expired. Start again." });
            return Ok(new { Message = "Your password has been updated. You can now log in." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    // Records why the email failed
    private async Task LogDeliveryFailureAsync(string step, ConfirmationDeliveryException ex)
    {
        var cause = ex.InnerException ?? ex;
        _logger.LogError(cause, "Confirmation email failed during {Step}.", step);
        await _devLogs.TryWriteAsync(new DeveloperLog
        {
            Actor = "Anonymous",
            Action = "EMAIL_SEND_FAILED",
            EntityType = "Users",
            EntityId = step,
            Severity = "Error",
            Details = $"Confirmation email could not be sent during {step}.\n" +
                      $"{cause.GetType().Name}: {cause.Message}" +
                      (cause.InnerException is null ? string.Empty
                          : $"\nCaused by {cause.InnerException.GetType().Name}: {cause.InnerException.Message}") +
                      "\n\nCheck the Email settings in Azure (Host, Port 587, EnableSsl, UserName, Password, FromAddress) " +
                      "and that the FromAddress domain is verified in Resend.",
            LoggedAt = DateTime.UtcNow
        });
    }
}
