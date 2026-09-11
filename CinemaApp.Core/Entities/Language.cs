namespace CinemaApp.Core.Entities;

public class Language
{
    public int Id { get; set; }
    public required string CultureCode { get; set; } = string.Empty; // e.g. "en-US", "hy-AM", "ru-RU"
    public required string DisplayName { get; set; } = string.Empty; // e.g. "English", "Հայերեն", "Русский"
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;

    public ICollection<MovieTranslation> MovieTranslations { get; set; } = new List<MovieTranslation>();
    public ICollection<CategoryTranslation> CategoryTranslations { get; set; } = new List<CategoryTranslation>();
}
