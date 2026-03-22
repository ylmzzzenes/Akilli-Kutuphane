namespace AkilliKutuphane.Business.Models;

public class BookCardDto
{
    public int? LocalBookId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int? FirstPublishYear { get; set; }
    public bool IsFavorite { get; set; }
    public double AverageRating { get; set; }
}
