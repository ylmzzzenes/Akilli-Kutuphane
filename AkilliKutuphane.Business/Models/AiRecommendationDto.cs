namespace AkilliKutuphane.Business.Models;

public class AiRecommendationDto
{
    public BookCardDto Book { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}
