using backend.Database;
using backend.DTOs.Auth;
using backend.Entities;
using backend.Services;
using backend.Settings;
using backend.Extensions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext dbContext,
    ApplicationIdentityDbContext identityDbContext,
    TokenProvider tokenProvider,
    UserContext userContext,
    ILogger<AuthController> logger,
    IOptions<JwtAuthOptions> options) : ControllerBase
{
    private readonly JwtAuthOptions _jwtAuthOptions = options.Value;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserDto dto,
        [FromServices] IValidator<LoginUserDto> validator)
    {
        ValidationResult validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return this.ValidationFailed(validation);
        }

        // 1. identity schema — credentials.
        // same generic message whether the email is unknown or the password is wrong
        // (don't reveal which emails exist)
        IdentityUser? identityUser = await userManager.FindByEmailAsync(dto.Email);
        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, dto.Password))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "Invalid email or password.");
        }

        // 2. app schema — the domain user, its role and active state
        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.IdentityId == identityUser.Id);
        if (user is null)
        {
            // A credential with no domain record is a seeding/provisioning bug, not
            // a caller error — but saying so out loud confirms the email exists.
            // Log it, return the same generic 401.
            logger.LogError(
                "Identity user {IdentityId} has no matching domain user.", identityUser.Id);

            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "This account has been deactivated.");
        }

        // sub = the DOMAIN user id, so no lookup is ever needed to answer "who is
        // this?". The role claim must be the lowercase wire value: [Authorize(Roles
        // = "admin")] is an exact string match, and "Admin" would match nothing.
        var tokenRequest = new TokenRequest(
            user.Id,
            user.Email,
            [user.Role.ToClaimValue()]);

        AccessTokensDto tokens = tokenProvider.Create(tokenRequest);

        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,   // refresh tokens belong to the credential
            Token = tokens.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationDays)
        };
        identityDbContext.RefreshTokens.Add(refreshToken);
        await identityDbContext.SaveChangesAsync();

        return Ok(tokens);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        // The one place GetUserAsync is the right call: /me exists to return the
        // record, and it's read on nearly every page load, so the cache pays.
        User? user = await userContext.GetUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = new CurrentUserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive
        };

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        string? userId = userContext.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // `sub` is the domain id but refresh tokens are keyed by the identity id,
        // so this hop is required. Cold path — one indexed lookup on logout.
        string? identityId = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IdentityId)
            .FirstOrDefaultAsync();

        if (identityId is null)
        {
            return Unauthorized();
        }

        // revoke every refresh token for this credential -> server session cleared
        await identityDbContext.RefreshTokens
            .Where(rt => rt.UserId == identityId)
            .ExecuteDeleteAsync();

        return NoContent();
    }
}
