using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Coinwarden.API.Controllers;
using Coinwarden.API.Core;
using Coinwarden.API.Services.Core;

namespace Coinwarden.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="ServiceControllerBase.HandleServiceFailureResult{T}"/>.
/// </summary>
public sealed class ServiceControllerBaseTests
{
    private readonly TestController sut;

    public ServiceControllerBaseTests()
    {
        var correlationIdService = new Mock<ICorrelationIdService>();
        correlationIdService.Setup(s => s.CorrelationId).Returns("test-correlation-id");

        this.sut = new TestController(correlationIdService.Object);
        this.sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public void HandleServiceFailureResult_NotFound_ReturnsNotFoundResult()
    {
        var result = Result<string>.Failure(DomainErrorType.NotFound, "not found");
        var actionResult = this.sut.InvokeHandleServiceFailureResult(result);
        Assert.Equal(StatusCodes.Status404NotFound, actionResult.StatusCode);
    }

    [Fact]
    public void HandleServiceFailureResult_Validation_ReturnsBadRequest()
    {
        var result = Result<string>.Failure(DomainErrorType.Validation, "invalid input");
        var actionResult = this.sut.InvokeHandleServiceFailureResult(result);
        Assert.Equal(StatusCodes.Status400BadRequest, actionResult.StatusCode);
    }

    [Fact]
    public void HandleServiceFailureResult_Unauthorized_ReturnsUnauthorized()
    {
        var result = Result<string>.Failure(DomainErrorType.Unauthorized, "unauthorized");
        var actionResult = this.sut.InvokeHandleServiceFailureResult(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, actionResult.StatusCode);
    }

    [Fact]
    public void HandleServiceFailureResult_Forbidden_ReturnsForbidden()
    {
        var result = Result<string>.Failure(DomainErrorType.Forbidden, "forbidden");
        var actionResult = this.sut.InvokeHandleServiceFailureResult(result);
        Assert.Equal(StatusCodes.Status403Forbidden, actionResult.StatusCode);
    }

    [Fact]
    public void HandleServiceFailureResult_Conflict_ReturnsConflict()
    {
        var result = Result<string>.Failure(DomainErrorType.Conflict, "conflict");
        var actionResult = this.sut.InvokeHandleServiceFailureResult(result);
        Assert.Equal(StatusCodes.Status409Conflict, actionResult.StatusCode);
    }

    [Fact]
    public void HandleServiceFailureResult_Unknown_ReturnsInternalServerError()
    {
        var result = Result<string>.Failure(DomainErrorType.Unknown, "unknown error");
        var actionResult = this.sut.InvokeHandleServiceFailureResult(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, actionResult.StatusCode);
    }

    [Fact]
    public void HandleServiceFailureResult_SuccessResult_ThrowsInvalidOperationException()
    {
        var result = Result<string>.Success("ok");
        Assert.Throws<System.InvalidOperationException>(() => this.sut.InvokeHandleServiceFailureResult(result));
    }

    private sealed class TestController : ServiceControllerBase
    {
        public TestController(ICorrelationIdService correlationIdService) : base(correlationIdService)
        {
        }

        public ObjectResult InvokeHandleServiceFailureResult<T>(Result<T> result)
        {
            return this.HandleServiceFailureResult(result);
        }
    }
}
