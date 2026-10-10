using System;
using System.Threading.Tasks;
using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Moq;
using Xunit;

namespace CivicConnect.Tests.Unit
{
    public class RequestServiceUnitTests
    {
        private readonly Mock<IRequestRepository> _mockRepo;
        private readonly RequestService _service;

        public RequestServiceUnitTests()
        {
            _mockRepo = new Mock<IRequestRepository>();
            _service = new RequestService(_mockRepo.Object);
        }

        [Fact]
        public async Task Request_Submission_For_Empty_Description_()
        {
            var invalidRequest = new ServiceRequest
            {
                Username = "Aiden Moses",
                Category = "Water & Sanitation",
                Description = "   "
            };

            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.RequestSubmission(invalidRequest)
            );
        }

        [Fact]
        public async Task Update_Request_Status_Valid_Status()
        {
            int ticketId = 101;
            
            _mockRepo.Setup(r => r.UpdateRequestStatus(ticketId, "In Progress")).ReturnsAsync(true);

            await _service.UpdateRequestStatus(ticketId, "In Progress");

            _mockRepo.Verify(r => r.UpdateRequestStatus(ticketId, "In Progress"), Times.Once);
        }

        [Fact]
        public async Task Update_Request_Status_Invalid_Status()
        {
            int ticketId = 102;

            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.UpdateRequestStatus(ticketId, "IllegalStatus")
            );
        }
    }
}