using System;
using Coinwarden.API.Models.Responses;
using Coinwarden.API.Services.Auth;
using Coinwarden.API.Services.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Coinwarden.API.Controllers.V1;

/// <summary>
/// Hello controller for API v1.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/hello")]
public sealed class HelloController : ServiceControllerBase
{
    private readonly ICurrentUserService currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HelloController"/> class.
    /// </summary>
    /// <param name="currentUserService">The current user service.</param>
    /// <param name="correlationIdService">The correlation ID service.</param>
    public HelloController(ICurrentUserService currentUserService, ICorrelationIdService correlationIdService)
        : base(correlationIdService)
    {
        this.currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <summary>
    /// Returns a greeting for the authenticated user.
    /// </summary>
    /// <returns>A <see cref="HelloResponse"/> with a greeting and version.</returns>
    /// <response code="200">Returns the greeting.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet(Name = nameof(HelloV1Async))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<HelloResponse> HelloV1Async()
    {
        return this.Ok(new HelloResponse
        {
            Message = $"Hello, {this.currentUserService.UserName}! Welcome to Coinwarden API v1.",
            Version = "v1",
            UserName = this.currentUserService.UserName ?? "Unknown"
        });
    }

    /// <summary>
    /// Anonymous ping endpoint for liveness checks.
    /// </summary>
    /// <returns>A pong string.</returns>
    /// <response code="200">Returns "pong v1".</response>
    [AllowAnonymous]
    [HttpGet("ping", Name = nameof(PingV1))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> PingV1()
    {
        return this.Ok("pong v1");
    }
}
