using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Web.Models.Books;

public class BookListPageViewModel
{
    public string? Query { get; set; }
    public string? Author { get; set; }
    public PagedResultDto<BookCardDto> Result { get; set; } = new();
}
