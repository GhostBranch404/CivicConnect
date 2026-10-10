using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService services;

        public RequestController(IRequestService service)
        {
            services = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceRequest request)
        {
            try
            {
                int requestId = await services.RequestSubmission(request);
                return CreatedAtAction(nameof(GetById), new { id = requestId }, new { RequestId = requestId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var request = await services.GetRequestById(id);
            if (request == null) return NotFound(new { Message = $"Request #{id} was not found." });
            return Ok(request);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var requests = await services.GetAllRequests();
            return Ok(requests);
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
        {
            try
            {
                bool updated = await services.UpdateRequestStatus(id, newStatus);
                if (!updated) return NotFound(new { Message = $"Request #{id} was not found." });
                return Ok(new { Message = $"Request #{id} updated to '{newStatus}' successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}