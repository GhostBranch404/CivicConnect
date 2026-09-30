using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Core.Controllers
{
// RESTful API Controller responsible for managing the external HTTP requests for service requests
    [ApiController]
    [Route("api/[controller]")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _service;

        public RequestController(IRequestService service)
        {
            _service = service;
        }

// Responsible for managing the endpoint for users to submit new requests
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
        }

// Endpoint for the query status by request ID
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
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

// Restricts access to status updates to only authorized staff members
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] UpdateStatusRequest update)
        {
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
