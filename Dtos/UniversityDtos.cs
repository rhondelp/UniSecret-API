using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Dtos;

//DTO for returning university details to clients
public record UniversityDto(
    int Id,
    string Name,
    string Domain,
    string? LogoUrl,
    UniversityStatus Status,
    DateTime CreatedAt
);

public record CreateUniversityDto(
    [Required] [StringLength(150)] string Name,
    [Required] [StringLength(100)] string Domain,
    string? LogoUrl
);

//DTO for updating a universirt (Admin)
public record UpdateUniversityDto(
    [Required] [StringLength(150)] string Name,
    [Required] [StringLength(100)] string Domain,
    string? LogoUrl,
    UniversityStatus Status
);