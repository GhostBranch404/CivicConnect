using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Moq;

namespace CivicConnect.Tests;

public class WhiteBoxTestCase
{
    [Fact]
    public async Task UpdateRequestStatus_ResolvedWithoutNotes_RejectsBeforeRepositoryAccess()
    {
        var repository = new Mock<IRequestRepository>(MockBehavior.Strict);
        var service = new RequestService(repository.Object);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateRequestStatus(15, "Resolved", 3, 1));

        Assert.Equal("Resolution notes are required when resolving a request.", exception.Message);
        repository.VerifyNoOtherCalls();
    }
}