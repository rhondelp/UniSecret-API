using Microsoft.AspNetCore.Mvc;
using UniSecretApi.Dtos;
using UniSecretApi.Services;

namespace UniSecretApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    // POST: api/v1/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        var (success, message, data) = await _authService.RegisterAsync(dto);

        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new { message, data });
    }

    // POST: api/v1/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var (success, message, data) = await _authService.LoginAsync(dto);

        if (!success)
        {
            return Unauthorized(new { message });
        }

        return Ok(new { message, data });
    }
}   