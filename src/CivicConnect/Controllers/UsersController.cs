using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Core.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _users;

        public UsersController(IUserService users)
        {
            _users = users;
        }

        // GET /api/users            -> everyone
        // GET /api/users?role=Staff -> only staff (used for the "Assign to" list)
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? role) =>
            Ok(await _users.GetUsersAsync(role));
    }
}
