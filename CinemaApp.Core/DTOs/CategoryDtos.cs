namespace CinemaApp.Core.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class LanguageDto
{
    public int Id { get; set; }
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

public class AddLanguageDto
{
    public required string CultureCode { get; set; } = string.Empty;
    public required string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
}
