namespace AkilliKutuphane.Business.Models;

public class RatedBookDto
{
    public BookCardDto Book { get; set; } = new();
    public int UserScore { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
