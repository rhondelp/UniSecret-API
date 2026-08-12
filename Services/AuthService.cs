using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UniSecretApi.Data;
using UniSecretApi.Dtos;
using UniSecretApi.Entities;
using UniSecretApi.Enums;

namespace UniSecretApi.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(bool Success, string Message, AuthResponseDto? Data)> RegisterAsync(RegisterDto dto)
    {
        // 1. Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
        {
            return (false, "Email is already registered.", null);
        }

        // 2. Check if username already exists
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
        {
            return (false, "Username is already taken.", null);
        }

        // 3. Verify University exists
        var university = await _context.Universities.FindAsync(dto.UniversityId);
        if (university is null)
        {
            return (false, "Selected university does not exist.", null);
        }

        // 4. Optional: Check if email domain matches university domain (e.g., student@harvard.edu matches harvard.edu)
        var emailDomain = dto.Email.Split('@').Last().ToLower();
        if (!emailDomain.EndsWith(university.Domain.ToLower()))
        {
            return (false, $"Email domain ({emailDomain}) does not match the university's domain ({university.Domain}).", null);
        }

        // 5. Hash password
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // 6. Create User Entity
        var user = new User
        {
            UniversityId = dto.UniversityId,
            Name = dto.Name,
            Username = dto.Username,
            Email = dto.Email.ToLower(),
            PasswordHash = passwordHash,
            Role = UserRole.Student,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 7. Generate JWT Token
        string token = GenerateJwtToken(user);

        var response = new AuthResponseDto(
            user.Id,
            user.Name,
            user.Username,
            user.Email,
            token
        );

        return (true, "Registration successful.", response);
    }

    public async Task<(bool Success, string Message, AuthResponseDto? Data)> LoginAsync(LoginDto dto)
    {
        // 1. Find user by email
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

        if (user is null)
        {
            return (false, "Invalid email or password.", null);
        }

        // 2. Verify password
        bool isValidPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isValidPassword)
        {
            return (false, "Invalid email or password.", null);
        }

        // 3. Check if user is banned/suspended
        if (user.Status != UserStatus.Active)
        {
            return (false, $"Your account is {user.Status.ToString().ToLower()}. Contact support.", null);
        }

        // 4. Generate JWT Token
        string token = GenerateJwtToken(user);

        var response = new AuthResponseDto(
            user.Id,
            user.Name,
            user.Username,
            user.Email,
            token
        );

        return (true, "Login successful.", response);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured.");
        var key = Encoding.ASCII.GetBytes(secretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("university_id", user.UniversityId.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(double.Parse(jwtSettings["DurationInDays"] ?? "7")),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}