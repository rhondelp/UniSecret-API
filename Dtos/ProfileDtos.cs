using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Dtos;

public record UserProfileDto(
    int Id,
    string Name,
    string Username,
    string? AvatarUrl,
    int UniversityId,
    string UniversityName,
    DateTime JoinedAt
);

public record UpdateProfileDto(
    [StringLength(100)] string? Name = null,
    [StringLength(50)] string? Username = null,
    [EmailAddress] [StringLength(150)] string? Email = null
);

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required] [MinLength(6)] string NewPassword
);
