using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceCenter.Web.Models;
using ServiceCenter.Web.Services;

namespace ServiceCenter.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Login([FromBody] LoginRequest request)
    {
        var user = _auth.Authenticate(request.Email, request.Password);
        await SignInCookieAsync(user);
        return Ok(user);
    }

    [HttpPost("auth/register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request)
    {
        var user = _auth.RegisterClient(request);
        await SignInCookieAsync(user);
        return Created("/api/auth/me", user);
    }

    [HttpPost("auth/logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("auth/me")]
    [Authorize]
    public ActionResult<UserDto> Me()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idStr, out var userId))
            return Unauthorized();

        var user = _auth.GetUserById(userId);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpGet("users/technicians")]
    [Authorize(Roles = UserRoles.Receiver)]
    public ActionResult<IReadOnlyList<UserDto>> GetTechnicians()
    {
        return Ok(_auth.GetUsersByRole(UserRoles.Technician));
    }

    [HttpGet("users/clients")]
    [Authorize(Roles = UserRoles.Receiver)]
    public ActionResult<IReadOnlyList<UserDto>> GetClients()
    {
        return Ok(_auth.GetUsersByRole(UserRoles.Client));
    }

    private async Task SignInCookieAsync(UserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
    }
}
