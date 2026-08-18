using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Dtos;

public record CreateReportDto(
    [Required] int ReportableId,
    [Required] string ReportableType,
    [Required] string Reason,        
    string? Details
);


public record ReportDto(
    int Id,
    int ReporterId,
    string ReporterUsername,
    int ReportableId,
    string ReportableType,
    string Reason,
    string Status,
    DateTime CreatedAt
);

public record ReviewConfessionDto(
    [Required] bool Approve,
    string? Reason
);

public record UpdateUserStatusDto(
    [Required] UserStatus Status,
    string? Reason
);

public record ModerationLogDto(
    int Id,
    int ModeratorId,
    string ModeratorUsername,
    string Action,
    string TargetType,
    int TargetId,
    string? Reason,
    DateTime CreatedAt
);