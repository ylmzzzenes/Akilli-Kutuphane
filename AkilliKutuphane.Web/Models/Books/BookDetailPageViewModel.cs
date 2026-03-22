using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Web.Models.Books;

public class BookDetailPageViewModel
{
    public BookDetailDto Book { get; set; } = new();
    public string? ReturnUrl { get; set; }
}
