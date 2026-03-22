namespace AkilliKutuphane.Business.Services.Interfaces;

public interface IRatingService
{
    Task UpsertRatingAsync(string userId, string externalBookId, int score, CancellationToken cancellationToken = default);
}
