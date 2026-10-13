using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Moq;

namespace CivicConnect.Tests;

public class RequestAdditionalUnitTests
{
    [Fact]
    public async Task RequestSubmission_For_ValidRequest()
    {
        var repository = new Mock<IRequestRepository>();
        var auditHistory = new Mock<IAuditHistoryService>();
        repository.Setup(repo => repo.CreateNewRequest(It.IsAny<ServiceRequest>()))
            .ReturnsAsync(27);
        var service = new RequestService(repository.Object, auditHistory.Object);
        var request = new ServiceRequest
        {
            RequesterId = 8,
            CategoryId = 4,
            Title = "Broken street light",
            Description = "The light is out.",
            Location = "Main Street"
        };

        var requestId = await service.RequestSubmission(request);

        Assert.Equal(27, requestId);
        Assert.Equal(1, request.StatusId);
        Assert.Equal(1, request.Version);
        Assert.Equal(request.RequestDate, request.UpdatedAt);
        repository.Verify(repo => repo.CreateNewRequest(request), Times.Once);
    }

    [Fact]
    public async Task UpdateRequestStatus_For_A_Request_ThatDoesNotExist()
    {
        var repository = new Mock<IRequestRepository>();
        var auditHistory = new Mock<IAuditHistoryService>();
        repository.Setup(repo => repo.GetRequestById(404))
            .ReturnsAsync((ServiceRequest?)null);
        var service = new RequestService(repository.Object, auditHistory.Object);

        var updated = await service.UpdateRequestStatus(404, "In Progress", 3, 1);

        Assert.False(updated);
        repository.Verify(repo => repo.GetStatusIdByNameAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(repo => repo.UpdateRequestStatus(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
        auditHistory.Verify(audit => audit.RecordChangeAsync(It.IsAny<RequestHistory>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRequestStatus_Invalid_User()
    {
        var repository = new Mock<IRequestRepository>(MockBehavior.Strict);
        var auditHistory = new Mock<IAuditHistoryService>(MockBehavior.Strict);
        var service = new RequestService(repository.Object, auditHistory.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateRequestStatus(15, "In Progress", 0, 1));

        repository.VerifyNoOtherCalls();
        auditHistory.VerifyNoOtherCalls();
    }
}
