using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Dtos;

public record CreateConfessionDto(
    int? UniversityId,
    [Required] int CategoryId,
    [Required] string Body,
    bool IsAnonymous = true,
    DateTime? ScheduledAt = null,
    string? ImageUrl = null
);

public record UpdateConfessionDto(
    [Required] int CategoryId,
    [Required] string Body,
    bool IsAnonymous = false,
    string? ImageUrl = null
);

public record ConfessionDto(
    int Id,
    int UniversityId,
    int CategoryId,
    string CategoryName,
    string Body,
    bool IsAnonymous,
    string AuthorName, 
    string AuthorUsername,
    ConfessionStatus Status,
    DateTime? ScheduledAt,
    DateTime CreatedAt,
    int LikesCount = 0,
    bool IsLiked = false,
    bool IsSaved = false,
    string? ImageUrl = null,
    int CommentCount = 0,
    int ShareCount = 0,
    ReactionType? UserReaction = null
);