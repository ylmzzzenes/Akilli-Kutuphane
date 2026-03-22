using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Business.Services.Interfaces;

public interface IFavoriteService
{
    Task AddFavoriteAsync(string userId, string externalBookId, CancellationToken cancellationToken = default);
    Task RemoveFavoriteAsync(string userId, string externalBookId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookCardDto>> GetFavoritesAsync(string userId, CancellationToken cancellationToken = default);
}
