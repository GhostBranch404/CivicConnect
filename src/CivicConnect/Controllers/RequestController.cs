using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace CivicConnect.Core.Controllers
{
    [ApiController]
    [Route("api/requests")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _service;

        public RequestController(IRequestService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceRequest request)
        {
            try
            {
                int requestId = await _service.RequestSubmission(request);
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = requestId },
                    new { RequestId = requestId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict(new { Message = "A request with the same ticket number already exists." });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new { Message = "The requester or category does not exist." });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
                return BadRequest(new { Message = "A valid request identifier is required." });

            var request = await _service.GetRequestById(id);
            if (request == null)
                return NotFound(new { Message = $"Request #{id} was not found." });

            return Ok(request);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var requests = await _service.GetAllRequests();
            return Ok(requests);
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest update)
        {
            if (id <= 0)
                return BadRequest(new { Message = "A valid request identifier is required." });

            try
            {
                bool updated = await _service.UpdateRequestStatus(
                    id,
                    update.NewStatus,
                    update.ChangedByUserId,
                    update.ExpectedVersion,
                    update.Notes);

                if (!updated)
                    return NotFound(new { Message = $"Request #{id} was not found." });

                return Ok(new
                {
                    Message = $"Request #{id} updated to '{update.NewStatus}' successfully."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { Message = ex.Message });
            }
        }
    }
}
