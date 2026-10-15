using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace CivicConnect.Core.Controllers
{
    // User management for Admins and Devs. Every call says which user is acting;
    // the service checks that user is an active Admin or Dev.
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _admin;

        public AdminController(IAdminService admin)
        {
            _admin = admin;
        }

        // GET /api/admin/users?actingUserId=5&name=sarah&role=Staff
        [HttpGet("users")]
        public Task<IActionResult> Search([FromQuery] int actingUserId, [FromQuery] string? name, [FromQuery] string? role) =>
            Run(async () => Ok(await _admin.SearchUsersAsync(actingUserId, name, role)));

        [HttpPost("users/{id:int}/promote")]
        public Task<IActionResult> Promote(int id, [FromBody] ActingUserRequest body) =>
            Run(async () => Accepted(await _admin.RequestPromoteAsync(body.ActingUserId, id)));

        [HttpPost("users/{id:int}/remove-access")]
        public Task<IActionResult> RemoveAccess(int id, [FromBody] ActingUserRequest body) =>
            Run(async () => Accepted(await _admin.RequestRemoveAccessAsync(body.ActingUserId, id)));

        [HttpPost("users")]
        public Task<IActionResult> Create([FromBody] AdminCreateUserRequest body) =>
            Run(async () =>
            {
                var result = await _admin.CreateUserAsync(body);
                return result.Pending ? Accepted(result) : StatusCode(StatusCodes.Status201Created, result);
            });

        // GET /api/admin/approvals?actingUserId=5
        [HttpGet("approvals")]
        public Task<IActionResult> Approvals([FromQuery] int actingUserId) =>
            Run(async () => Ok(await _admin.GetPendingApprovalsAsync(actingUserId)));

        [HttpPost("approvals/{id:int}/approve")]
        public Task<IActionResult> Approve(int id, [FromBody] ActingUserRequest body) =>
            Run(async () => Ok(await _admin.ApproveAsync(body.ActingUserId, id)));

        [HttpPost("approvals/{id:int}/decline")]
        public Task<IActionResult> Decline(int id, [FromBody] ActingUserRequest body) =>
            Run(async () => Ok(await _admin.DeclineAsync(body.ActingUserId, id)));

        private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
            catch (MySqlException ex) when (ex.Number == 1146 || ex.Number == 1054)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Message = "User management isn't set up in the database yet. Run database/migrations/004_admin_approvals.sql."
                });
            }
        }
    }
}
