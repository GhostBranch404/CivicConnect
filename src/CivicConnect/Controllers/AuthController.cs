using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace CivicConnect.Core.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private const string MigrationHint =
            "User accounts are not set up in the database yet. Run database/migrations/002_user_accounts.sql.";

        private readonly IUserService _users;

        public AuthController(IUserService users)
        {
            _users = users;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var user = await _users.RegisterAsync(request);
                return StatusCode(StatusCodes.Status201Created, user);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { Message = ex.Message });
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict(new { Message = "An account with this email already exists." });
            }
            catch (MySqlException ex) when (ex.Number == 1054)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = MigrationHint });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _users.LoginAsync(request);
                if (user is null)
                    return Unauthorized(new { Message = "Incorrect email or password." });

                return Ok(user);
            }
            catch (MySqlException ex) when (ex.Number == 1054)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = MigrationHint });
            }
        }
    }
}
