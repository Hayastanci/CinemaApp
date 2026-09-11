namespace CinemaApp.Core.Entities;

public class CategoryTranslation
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int LanguageId { get; set; }
    public Language Language { get; set; } = null!;

    public required string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
