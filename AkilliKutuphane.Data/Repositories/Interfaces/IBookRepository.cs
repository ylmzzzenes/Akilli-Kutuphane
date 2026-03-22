using AkilliKutuphane.Data.Entities;

namespace AkilliKutuphane.Data.Repositories.Interfaces;

public interface IBookRepository
{
    Task<Book?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Book>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<Book> UpsertExternalBookAsync(Book input, CancellationToken cancellationToken = default);
}
