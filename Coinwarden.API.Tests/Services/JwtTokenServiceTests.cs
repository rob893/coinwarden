using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Coinwarden.API.Constants;
using Coinwarden.API.Data.Repositories;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.Settings;
using Coinwarden.API.Services.Auth;

namespace Coinwarden.API.Tests.Services;

/// <summary>
/// Tests for <see cref="JwtTokenService"/>.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private const string ValidSecret = "this-is-a-sufficiently-long-api-secret-of-at-least-64-characters!!";
    private const string Issuer = "https://issuer.example.com";
    private const string Audience = "https://audience.example.com";
    private const string DeviceId = "device-123";

    private readonly Mock<IUserRepository> userRepositoryMock;

    public JwtTokenServiceTests()
    {
        this.userRepositoryMock = new Mock<IUserRepository>();
        this.userRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task RefreshToken_RoundTrip_IsAccepted()
    {
        var sut = this.CreateSut();
        var user = BuildUser();

        var refreshToken = await sut.GenerateAndSaveRefreshTokenForUserAsync(user, DeviceId, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        Assert.Single(user.RefreshTokens);

        this.userRepositoryMock
            .Setup(r => r.GetRefreshTokensForDeviceAsync(DeviceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.RefreshTokens);
        this.userRepositoryMock
            .Setup(r => r.GetUserForTokenRefreshAsync(It.IsAny<int>(), DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var (isEligible, eligibleUser) = await sut.IsTokenEligibleForRefreshAsync(refreshToken, DeviceId, CancellationToken.None);

        Assert.True(isEligible);
        Assert.Same(user, eligibleUser);
        this.userRepositoryMock.Verify(r => r.DeleteExpiredRefreshTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_WrongToken_IsRejected()
    {
        var sut = this.CreateSut();
        var user = BuildUser();

        await sut.GenerateAndSaveRefreshTokenForUserAsync(user, DeviceId, CancellationToken.None);

        this.userRepositoryMock
            .Setup(r => r.GetRefreshTokensForDeviceAsync(DeviceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.RefreshTokens);

        var (isEligible, eligibleUser) = await sut.IsTokenEligibleForRefreshAsync("garbage-token-value", DeviceId, CancellationToken.None);

        Assert.False(isEligible);
        Assert.Null(eligibleUser);
        this.userRepositoryMock.Verify(r => r.GetUserForTokenRefreshAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsTokenEligibleForRefreshAsync_NoTokensForDevice_ReturnsFalse()
    {
        var sut = this.CreateSut();
        this.userRepositoryMock
            .Setup(r => r.GetRefreshTokensForDeviceAsync(DeviceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var (isEligible, eligibleUser) = await sut.IsTokenEligibleForRefreshAsync("any-token", DeviceId, CancellationToken.None);

        Assert.False(isEligible);
        Assert.Null(eligibleUser);
    }

    [Fact]
    public async Task IsTokenEligibleForRefreshAsync_ExpiredMatchedToken_IsRejected()
    {
        var sut = this.CreateSut();
        var user = BuildUser();

        var refreshToken = await sut.GenerateAndSaveRefreshTokenForUserAsync(user, DeviceId, CancellationToken.None);
        foreach (var token in user.RefreshTokens)
        {
            token.Expiration = DateTimeOffset.UtcNow.AddMinutes(-5);
        }

        this.userRepositoryMock
            .Setup(r => r.GetRefreshTokensForDeviceAsync(DeviceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.RefreshTokens);

        var (isEligible, eligibleUser) = await sut.IsTokenEligibleForRefreshAsync(refreshToken, DeviceId, CancellationToken.None);

        Assert.False(isEligible);
        Assert.Null(eligibleUser);
        this.userRepositoryMock.Verify(r => r.DeleteExpiredRefreshTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        this.userRepositoryMock.Verify(r => r.GetUserForTokenRefreshAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsTokenEligibleForRefreshAsync_PrunesExpiredTokens()
    {
        var sut = this.CreateSut();
        var user = BuildUser();

        var refreshToken = await sut.GenerateAndSaveRefreshTokenForUserAsync(user, DeviceId, CancellationToken.None);

        // The lean device query returns only this device's (valid) token; expired tokens for other
        // devices are pruned by the targeted DeleteExpiredRefreshTokensAsync delete.
        this.userRepositoryMock
            .Setup(r => r.GetRefreshTokensForDeviceAsync(DeviceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user.RefreshTokens);
        this.userRepositoryMock
            .Setup(r => r.GetUserForTokenRefreshAsync(It.IsAny<int>(), DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var (isEligible, eligibleUser) = await sut.IsTokenEligibleForRefreshAsync(refreshToken, DeviceId, CancellationToken.None);

        Assert.True(isEligible);
        Assert.Same(user, eligibleUser);
        this.userRepositoryMock.Verify(r => r.DeleteExpiredRefreshTokensAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAndSaveRefreshTokenForUserAsync_RemovesExistingTokenForSameDevice()
    {
        var sut = this.CreateSut();
        var user = BuildUser();
        user.RefreshTokens.Add(new RefreshToken
        {
            DeviceId = DeviceId,
            Expiration = DateTimeOffset.UtcNow.AddMinutes(60)
        });

        await sut.GenerateAndSaveRefreshTokenForUserAsync(user, DeviceId, CancellationToken.None);

        Assert.Single(user.RefreshTokens);
        this.userRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GenerateJwtTokenForUser_ProducesTokenThatValidatesWithMatchingParameters()
    {
        var sut = this.CreateSut();
        var user = BuildUser();
        user.EmailConfirmed = true;
        user.UserRoles.Add(new UserRole { Role = new Role { Name = UserRoleName.Admin } });

        var token = sut.GenerateJwtTokenForUser(user);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidSecret)),
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

        var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.Equal(user.Email, principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("true", principal.FindFirst(AppClaimTypes.EmailVerified)?.Value);
        Assert.True(principal.IsInRole(UserRoleName.Admin));
    }

    [Fact]
    public void Constructor_ShortSecret_ThrowsArgumentException()
    {
        var settings = BuildSettings(secret: "too-short-secret");

        Assert.Throws<ArgumentException>(() => new JwtTokenService(this.userRepositoryMock.Object, Options.Create(settings)));
    }

    [Fact]
    public async Task RevokeAllRefreshTokensForUserAsync_User_ClearsTokens()
    {
        var sut = this.CreateSut();
        var user = BuildUser();
        user.RefreshTokens.Add(new RefreshToken { DeviceId = "a" });
        user.RefreshTokens.Add(new RefreshToken { DeviceId = "b" });

        await sut.RevokeAllRefreshTokensForUserAsync(user, CancellationToken.None);

        Assert.Empty(user.RefreshTokens);
        this.userRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAllRefreshTokensForUserAsync_ById_ClearsTokens()
    {
        var sut = this.CreateSut();
        var user = BuildUser();
        user.RefreshTokens.Add(new RefreshToken { DeviceId = "a" });

        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<Expression<Func<User, object>>[]>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await sut.RevokeAllRefreshTokensForUserAsync(user.Id, CancellationToken.None);

        Assert.Empty(user.RefreshTokens);
    }

    [Fact]
    public async Task RevokeAllRefreshTokensForUserAsync_ById_UserNotFound_Throws()
    {
        var sut = this.CreateSut();
        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<Expression<Func<User, object>>[]>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.RevokeAllRefreshTokensForUserAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeRefreshTokenForDeviceAsync_RemovesOnlyMatchingDevice()
    {
        var sut = this.CreateSut();
        var user = BuildUser();
        user.RefreshTokens.Add(new RefreshToken { DeviceId = DeviceId });
        user.RefreshTokens.Add(new RefreshToken { DeviceId = "keep-me" });

        this.userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<Expression<Func<User, object>>[]>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await sut.RevokeRefreshTokenForDeviceAsync(user.Id, DeviceId, CancellationToken.None);

        Assert.Single(user.RefreshTokens);
        Assert.Equal("keep-me", user.RefreshTokens[0].DeviceId);
    }

    [Fact]
    public async Task GenerateAndSaveRefreshTokenForUserAsync_NullUser_Throws()
    {
        var sut = this.CreateSut();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GenerateAndSaveRefreshTokenForUserAsync(null!, DeviceId, CancellationToken.None));
    }

    private JwtTokenService CreateSut(string secret = ValidSecret)
    {
        return new JwtTokenService(this.userRepositoryMock.Object, Options.Create(BuildSettings(secret)));
    }

    private static AuthenticationSettings BuildSettings(string secret) => new()
    {
        APISecret = secret,
        TokenIssuer = Issuer,
        TokenAudience = Audience,
        TokenExpirationTimeInMinutes = 15,
        RefreshTokenExpirationTimeInMinutes = 60
    };

    private static User BuildUser() => new()
    {
        Id = 42,
        UserName = "jane.doe",
        Email = "jane.doe@example.com",
        RefreshTokens = []
    };
}
