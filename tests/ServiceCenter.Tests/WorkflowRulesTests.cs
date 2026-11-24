using ServiceCenter.Web.Models;
using ServiceCenter.Web.Services;

namespace ServiceCenter.Tests;

public sealed class WorkflowRulesTests
{
    [Fact]
    public void Client_CanView_OnlyOwnRequests()
    {
        var req = new ServiceRequestRecord { Id = 1, ClientId = 10, TechnicianId = 20 };

        Assert.True(WorkflowRules.CanView(10, UserRoles.Client, req));
        Assert.False(WorkflowRules.CanView(11, UserRoles.Client, req));
    }

    [Fact]
    public void Technician_CanView_OnlyAssignedRequests()
    {
        var req = new ServiceRequestRecord { Id = 1, ClientId = 10, TechnicianId = 20 };

        Assert.True(WorkflowRules.CanView(20, UserRoles.Technician, req));
        Assert.False(WorkflowRules.CanView(21, UserRoles.Technician, req));
    }

    [Fact]
    public void Receiver_CanView_AllRequests()
    {
        var req = new ServiceRequestRecord { Id = 1, ClientId = 10, TechnicianId = null };
        Assert.True(WorkflowRules.CanView(99, UserRoles.Receiver, req));
    }

    [Fact]
    public void ValidateTransition_Rejects_ClosingWithoutCompletionResult()
    {
        var req = new ServiceRequestRecord
        {
            Id = 1,
            ClientId = 10,
            TechnicianId = 20,
            Status = RequestStatuses.InProgress,
            Version = 3
        };

        var ex = Assert.Throws<ValidationAppException>(() =>
            WorkflowRules.ValidateStatusTransition(
                req, RequestStatuses.WorkDone, UserRoles.Technician, 20,
                completionResult: "   ", comment: null,
                workItemsCount: 1, unapprovedWorkItemsCount: 0));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void ValidateTransition_Rejects_ClosingUnapprovedWork()
    {
        var req = new ServiceRequestRecord
        {
            Id = 1,
            ClientId = 10,
            TechnicianId = 20,
            Status = RequestStatuses.InProgress,
            Version = 3
        };

        var ex = Assert.Throws<ConflictAppException>(() =>
            WorkflowRules.ValidateStatusTransition(
                req, RequestStatuses.WorkDone, UserRoles.Technician, 20,
                completionResult: "Готово", comment: null,
                workItemsCount: 2, unapprovedWorkItemsCount: 1));

        Assert.Equal("WORK_NOT_APPROVED", ex.ErrorCode);
    }

    [Fact]
    public void ValidateTransition_Rejects_DirectJumpFromNewToCompleted()
    {
        var req = new ServiceRequestRecord
        {
            Id = 1,
            ClientId = 10,
            Status = RequestStatuses.New,
            Version = 1
        };

        var ex = Assert.Throws<ConflictAppException>(() =>
            WorkflowRules.ValidateStatusTransition(
                req, RequestStatuses.Completed, UserRoles.Receiver, 1,
                completionResult: "Готово", comment: null,
                workItemsCount: 1, unapprovedWorkItemsCount: 0));

        Assert.Equal("INVALID_TRANSITION", ex.ErrorCode);
    }
}
