namespace AkilliKutuphane.Business.Models;

public class BookDetailDto
{
    public int? LocalBookId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public int? FirstPublishYear { get; set; }
    public bool IsFavorite { get; set; }
    public double AverageRating { get; set; }
    public int? UserRating { get; set; }
}
