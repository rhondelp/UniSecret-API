namespace UniSecretApi.Dtos;

public record ConfessionSearchQueryDto(
    string? Q = null,          
    int? UniversityId = null,  
    int? CategoryId = null,    
    string? Tag = null,        
    int Page = 1,
    int PageSize = 20
);