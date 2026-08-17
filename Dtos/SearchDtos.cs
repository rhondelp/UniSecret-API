namespace UniSecretApi.Dtos;

public record ConfessionSearchQueryDto(
    string? Q = null,           // Text query against confession body
    int? UniversityId = null,   // Filter by university
    int? CategoryId = null,     // Filter by category
    string? Tag = null,         // Filter by hashtag (e.g., "exams")
    int Page = 1,
    int PageSize = 20
);