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
        private readonly Mock<IAuditHistoryService> _mockAuditHistoryService;
        private readonly RequestService _service;

        public RequestServiceUnitTests()
        {
            _mockRepo = new Mock<IRequestRepository>();
            _mockAuditHistoryService = new Mock<IAuditHistoryService>();
            _service = new RequestService(_mockRepo.Object, _mockAuditHistoryService.Object);
        }

        [Fact]
        public async Task Request_Submission_For_Empty_Description_()
        {
            var invalidRequest = new ServiceRequest
            {
                RequesterId = 1,
                CategoryId = 1,
                Title = "Test request",
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
            int userId = 1;
            int version = 1;
            string newStatus = "In Progress";

            _mockRepo.Setup(r => r.UpdateRequestStatus(ticketId, newStatus, userId, version, null))
                     .ReturnsAsync(true);
            _mockRepo.Setup(r => r.GetRequestById(ticketId))
                     .ReturnsAsync(new ServiceRequest { RequestId = ticketId, StatusId = 1 });
            _mockRepo.Setup(r => r.GetStatusIdByNameAsync(newStatus))
                     .ReturnsAsync(2);

            await _service.UpdateRequestStatus(ticketId, newStatus, userId, version);

            _mockRepo.Verify(r => r.UpdateRequestStatus(ticketId, newStatus, userId, version, null), Times.Once);
            _mockAuditHistoryService.Verify(
                service => service.RecordChangeAsync(It.Is<RequestHistory>(history =>
                    history.RequestId == ticketId &&
                    history.OldStatusId == 1 &&
                    history.NewStatusId == 2)),
                Times.Once);
        }

        [Fact]
        public async Task Update_Request_Status_Invalid_Status()
        {
            int ticketId = 102;
            int userId = 1;
            int version = 1;

            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.UpdateRequestStatus(ticketId, "IllegalStatus", userId, version)
            );
        }
    }
}