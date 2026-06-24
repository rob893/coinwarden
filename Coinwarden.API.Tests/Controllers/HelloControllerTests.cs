using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Coinwarden.API.Controllers.V1;
using Coinwarden.API.Models.Responses;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;

namespace Coinwarden.API.Tests.Controllers;

/// <summary>
/// Tests for <see cref="HelloController"/> (v1).
/// </summary>
public sealed class HelloControllerTests
{
    private readonly Mock<ICurrentUserService> currentUserServiceMock;
    private readonly HelloController sut;

    public HelloControllerTests()
    {
        this.currentUserServiceMock = new Mock<ICurrentUserService>();
        this.currentUserServiceMock.Setup(s => s.UserName).Returns("testuser");

        var correlationIdService = new Mock<ICorrelationIdService>();
        correlationIdService.Setup(s => s.CorrelationId).Returns("corr-id");

        this.sut = new HelloController(this.currentUserServiceMock.Object, correlationIdService.Object);
        this.sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public void HelloV1Async_ReturnsOkWithHelloResponse()
    {
        var result = this.sut.HelloV1Async();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HelloResponse>(okResult.Value);
        Assert.Equal("v1", response.Version);
        Assert.Equal("testuser", response.UserName);
        Assert.Contains("testuser", response.Message);
    }

    [Fact]
    public void PingV1_ReturnsOkWithPongString()
    {
        var result = this.sut.PingV1();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("pong v1", okResult.Value);
    }
}
