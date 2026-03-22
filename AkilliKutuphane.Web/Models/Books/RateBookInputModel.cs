namespace AkilliKutuphane.Web.Models.Books;

public class RateBookInputModel
{
    public string ExternalId { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? ReturnUrl { get; set; }
}
