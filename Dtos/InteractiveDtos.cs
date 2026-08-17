using System.ComponentModel.DataAnnotations;

namespace UniSecretApi.Dtos;

// Comment DTOs
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
    [Required] string LikeableType // "Confession" or "Comment"
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