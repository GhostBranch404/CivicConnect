using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace CivicConnect.Core.Controllers
{
    [ApiController]
    [Route("api")]
    public class RequestDetailsController : ControllerBase
    {
        private readonly IRequestDetailsRepository _details;

        public RequestDetailsController(IRequestDetailsRepository details)
        {
            _details = details;
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories() =>
            Ok(await _details.GetCategoriesAsync());

        [HttpGet("statuses")]
        public async Task<IActionResult> GetStatuses() =>
            Ok(await _details.GetStatusesAsync());

        [HttpGet("requests/{id:int}/history")]
        public async Task<IActionResult> GetHistory(int id) =>
            Ok(await _details.GetHistoryAsync(id));

        [HttpGet("requests/{id:int}/comments")]
        public async Task<IActionResult> GetComments(int id) =>
            Ok(await _details.GetCommentsAsync(id));

        [HttpPost("requests/{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] NewCommentRequest comment)
        {
            if (comment.UserId <= 0)
                return BadRequest(new { Message = "A valid user is required." });
            if (string.IsNullOrWhiteSpace(comment.CommentText))
                return BadRequest(new { Message = "A comment cannot be empty." });

            try
            {
                var commentId = await _details.AddCommentAsync(id, comment.UserId, comment.CommentText.Trim());
                return StatusCode(StatusCodes.Status201Created, new { CommentId = commentId });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new { Message = "The request or user does not exist." });
            }
        }

        [HttpGet("assignments")]
        public async Task<IActionResult> GetAssignments() =>
            Ok(await _details.GetCurrentAssignmentsAsync());

        [HttpPut("requests/{id:int}/assignment")]
        public async Task<IActionResult> Assign(int id, [FromBody] AssignRequest assign)
        {
            if (assign.ChangedByUserId <= 0)
                return BadRequest(new { Message = "A valid acting user is required." });

            try
            {
                var ok = await _details.AssignAsync(id, assign.StaffUserId, assign.ChangedByUserId);
                return ok ? Ok(new { Message = "Assignment updated." })
                          : NotFound(new { Message = $"Request #{id} was not found." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
