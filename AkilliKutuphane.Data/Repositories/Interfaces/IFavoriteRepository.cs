namespace AkilliKutuphane.Data.Repositories.Interfaces;

public interface IFavoriteRepository
{
    Task<bool> IsFavoriteAsync(string userId, int bookId, CancellationToken cancellationToken = default);
    Task AddAsync(string userId, int bookId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string userId, int bookId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetFavoriteBookIdsAsync(string userId, CancellationToken cancellationToken = default);
}
