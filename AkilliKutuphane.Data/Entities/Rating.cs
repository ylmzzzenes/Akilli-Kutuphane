namespace AkilliKutuphane.Data.Entities;

public class Rating
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int BookId { get; set; }
    public int Score { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public Book Book { get; set; } = null!;
}
