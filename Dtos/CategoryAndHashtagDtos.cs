namespace UniSecretApi.Dtos;

public record CategoryDto(
    int Id,
    string Name,
    string Slug
);

public record HashtagDto(
    int Id,
    string Tag,
    int UsageCount
);