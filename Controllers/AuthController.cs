using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Models.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly FixitDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthController(FixitDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password" });

        var token = _jwt.GenerateToken(user);
        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName
        });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (await _db.AdminUsers.AnyAsync(u => u.Username == dto.Username))
            return BadRequest(new { message = "Username already exists" });

        var user = new AdminUser
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            FullName = dto.FullName
        };

        _db.AdminUsers.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { message = "User registered successfully" });
    }
}
