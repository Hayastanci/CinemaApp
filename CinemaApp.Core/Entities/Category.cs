namespace CinemaApp.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Slug { get; set; } = string.Empty;

    public ICollection<CategoryTranslation> CategoryTranslations { get; set; } = new List<CategoryTranslation>();
    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
}
