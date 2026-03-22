using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Business.Services.Interfaces;

public interface IRecommendationService
{
    Task<IReadOnlyList<AiRecommendationDto>> GetPersonalizedRecommendationsAsync(
        string userId,
        int take = 12,
        CancellationToken cancellationToken = default);

    Task InvalidateUserRecommendationsAsync(string userId);
}
