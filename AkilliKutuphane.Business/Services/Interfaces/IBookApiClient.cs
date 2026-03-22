using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Business.Services.Interfaces;

public interface IBookApiClient
{
    Task<PagedResultDto<BookCardDto>> SearchBooksAsync(string? query, string? author, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<BookDetailDto?> GetBookDetailAsync(string externalId, CancellationToken cancellationToken = default);
}
