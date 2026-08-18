using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Dtos;

public record CreateCommentDto(
    [Required] string Body,
    int? ParentId = null,
    bool IsAnonymous = false
);

public record CommentDto(
    int Id,
    int ConfessionId,
    int? ParentId,
    string Body,
    bool IsAnonymous,
    string AuthorName,
    string AuthorUsername,
    int LikeCount,
    DateTime CreatedAt,
    IReadOnlyList<CommentDto> Replies
);

// Like DTOs
public record ToggleLikeDto(
    [Required] int LikeableId,
    [Required] string LikeableType
);

public record LikeStatusDto(
    bool IsLiked,
    int TotalLikes
);

// Saved Post DTO
public record SavedPostDto(
    int Id,
    int ConfessionId,
    ConfessionDto Confession,
    DateTime SavedAt
);


public record SetReactionDto(
    [Required] int ReactableId,
    [Required] string ReactableType, 
    [Required] ReactionType Type
);

public record ReactionCountSummaryDto(
    ReactionType Type,
    int Count
);

public record ReactionUserDto(
    int UserId,
    string Name,
    string Username,
    ReactionType Type,
    DateTime ReactedAt
);
public record ReactionStatusDto(
    ReactionType? UserReaction,
    int TotalReactions,
    List<ReactionCountSummaryDto> Counts
);

public record CreateShareDto(
    [Required] int ConfessionId,
    string? Caption
);

public record ShareDto(
    int Id,
    int UserId,
    string UserName,
    int ConfessionId,
    ConfessionDto Confession,
    string? Caption,
    DateTime CreatedAt
);