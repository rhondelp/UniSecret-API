using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Dtos;

public record CreateConfessionDto(
    [Required] int UniversityId,
    [Required] int CategoryId,
    [Required] string Body,
    bool IsAnonymous = true,
    DateTime? ScheduledAt = null
);

public record ConfessionDto(
    int Id,
    int UniversityId,
    int CategoryId,
    string CategoryName,
    string Body,
    bool IsAnonymous,
    // If anonymous, hide author name/username from public responses
    string AuthorName, 
    string AuthorUsername,
    ConfessionStatus Status,
    DateTime? ScheduledAt,
    DateTime CreatedAt,
    int LikesCount = 0,
    bool IsLiked = false,
    bool IsSaved = false
);