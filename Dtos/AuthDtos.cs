using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Dtos;

//DTO for Registration
public record RegisterDto(
    [Required] int UniversityId,
    [Required] [StringLength(100)] string Name,
    [Required] [StringLength(50)] string Username,
    [Required] [EmailAddress] [StringLength(150)] string Email,
    [Required] [MinLength(6)] string Password
);

//DTO for Login
public record LoginDto(
    [Required] [EmailAddress] string Email,
    [Required]string Password
);

//DTO for Auth Response
public record AuthResponseDto(
    int Id,
    string Name,
    string Username,
    string Email,
    string Token
);

