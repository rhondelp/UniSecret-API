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

    public AuthService(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(
        bool Success,
        string Message,
        AuthResponseDto? Data)> RegisterAsync(
        RegisterDto dto,
        CancellationToken cancellationToken = default)
    {
        var email =
            dto.Email.Trim().ToLowerInvariant();

        var username =
            dto.Username.Trim().ToLowerInvariant();

        // --------------------------------------------------------
        // Check email.
        //
        // Email is normalized before querying, allowing PostgreSQL
        // to use the existing unique index directly.
        // --------------------------------------------------------

        if (await _context.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Email == email,
                cancellationToken))
        {
            return (
                false,
                "Email is already registered.",
                null);
        }

        // --------------------------------------------------------
        // Check username.
        // --------------------------------------------------------

        if (await _context.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.Username == username,
                cancellationToken))
        {
            return (
                false,
                "Username is already taken.",
                null);
        }

        // --------------------------------------------------------
        // Verify university.
        // Only the fields required for validation are selected.
        // --------------------------------------------------------

        var university =
            await _context.Universities
                .AsNoTracking()
                .Where(u => u.Id == dto.UniversityId)
                .Select(u => new
                {
                    u.Id,
                    u.Domain
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (university is null)
        {
            return (
                false,
                "Selected university does not exist.",
                null);
        }

        // --------------------------------------------------------
        // Verify email domain.
        // --------------------------------------------------------

        var emailDomain =
            email.Split('@').Last();

        if (!emailDomain.EndsWith(
                university.Domain,
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                false,
                $"Email domain ({emailDomain}) does not match " +
                $"the university's domain ({university.Domain}).",
                null);
        }

        // --------------------------------------------------------
        // BCrypt is CPU-intensive.
        //
        // It should NOT be placed inside a database transaction or
        // while holding a database connection.
        // --------------------------------------------------------

        var passwordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var now = DateTime.UtcNow;

        var user = new User
        {
            UniversityId = dto.UniversityId,
            Name = dto.Name.Trim(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Role = UserRole.Student,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique constraints remain the final protection
            // against concurrent duplicate registrations.
            //
            // The application-level AnyAsync checks above improve
            // the normal path, while the database constraint handles
            // race conditions between simultaneous requests.

            return (
                false,
                "The email or username is already registered.",
                null);
        }

        var token =
            GenerateJwtToken(user);

        var response =
            new AuthResponseDto(
                user.Id,
                user.Name,
                user.Username,
                user.Email,
                user.UniversityId,
                token);

        return (
            true,
            "Registration successful.",
            response);
    }

    public async Task<(
        bool Success,
        string Message,
        AuthResponseDto? Data)> LoginAsync(
        LoginDto dto,
        CancellationToken cancellationToken = default)
    {
        var email =
            dto.Email.Trim().ToLowerInvariant();

        // --------------------------------------------------------
        // Query only the user required for authentication.
        // --------------------------------------------------------

        var user =
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Email == email,
                    cancellationToken);

        if (user is null)
        {
            return (
                false,
                "Invalid email or password.",
                null);
        }

        // --------------------------------------------------------
        // BCrypt verification is intentionally CPU-intensive.
        // Rate limiting protects this endpoint from excessive
        // concurrent authentication attempts.
        // --------------------------------------------------------

        var isValidPassword =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if (!isValidPassword)
        {
            return (
                false,
                "Invalid email or password.",
                null);
        }

        if (user.Status != UserStatus.Active)
        {
            return (
                false,
                $"Your account is " +
                $"{user.Status.ToString().ToLowerInvariant()}. " +
                "Contact support.",
                null);
        }

        var token =
            GenerateJwtToken(user);

        var response =
            new AuthResponseDto(
                user.Id,
                user.Name,
                user.Username,
                user.Email,
                user.UniversityId,
                token);

        return (
            true,
            "Login successful.",
            response);
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(
        int userId,
        ChangePasswordDto dto,
        CancellationToken cancellationToken = default)
    {
        var user =
            await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Id == userId,
                    cancellationToken);

        if (user is null)
        {
            return (false, "User not found.");
        }

        var isCurrentPasswordValid =
            BCrypt.Net.BCrypt.Verify(
                dto.CurrentPassword,
                user.PasswordHash);

        if (!isCurrentPasswordValid)
        {
            return (false, "Current password is incorrect.");
        }

        if (BCrypt.Net.BCrypt.Verify(
                dto.NewPassword,
                user.PasswordHash))
        {
            return (false, "New password must be different from the current password.");
        }

        // BCrypt hashing is CPU-intensive and intentionally done
        // outside of any explicit transaction.

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return (true, "Password changed successfully.");
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings =
            _configuration.GetSection("JwtSettings");

        var secretKey =
            jwtSettings["Secret"]
            ?? throw new InvalidOperationException(
                "JWT Secret is not configured.");

        var key =
            Encoding.UTF8.GetBytes(secretKey);

        var duration =
            double.TryParse(
                jwtSettings["DurationInDays"],
                out var configuredDuration)
                ? configuredDuration
                : 7;

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Email,
                user.Email),

            new(
                ClaimTypes.Name,
                user.Username),

            new(
                ClaimTypes.Role,
                user.Role.ToString()),

            new(
                "university_id",
                user.UniversityId.ToString())
        };

        var tokenDescriptor =
            new SecurityTokenDescriptor
            {
                Subject =
                    new ClaimsIdentity(claims),

                Expires =
                    DateTime.UtcNow.AddDays(duration),

                Issuer =
                    jwtSettings["Issuer"],

                Audience =
                    jwtSettings["Audience"],

                SigningCredentials =
                    new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
            };

        var tokenHandler =
            new JwtSecurityTokenHandler();

        var token =
            tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}