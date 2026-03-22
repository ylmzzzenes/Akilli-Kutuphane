using AkilliKutuphane.Data.Entities;

namespace AkilliKutuphane.Data.Repositories.Interfaces;

public interface IRatingRepository
{
    Task<Rating?> GetUserRatingAsync(string userId, int bookId, CancellationToken cancellationToken = default);
    Task UpsertAsync(string userId, int bookId, int score, CancellationToken cancellationToken = default);
    Task<Dictionary<int, double>> GetAverageScoresAsync(IEnumerable<int> bookIds, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetUserRatingsMapAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Rating>> GetUserRatingsDetailedAsync(string userId, CancellationToken cancellationToken = default);
}
