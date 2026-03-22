using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Business.Services.Interfaces;

public interface IBookService
{
    Task<PagedResultDto<BookCardDto>> SearchBooksAsync(string? query, string? author, int page, int pageSize, string? userId, CancellationToken cancellationToken = default);
    Task<BookDetailDto?> GetBookDetailAsync(string externalId, string? userId, CancellationToken cancellationToken = default);
    Task<int?> EnsureLocalBookAsync(string externalId, CancellationToken cancellationToken = default);
}
