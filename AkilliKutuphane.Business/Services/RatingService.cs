using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Repositories.Interfaces;

namespace AkilliKutuphane.Business.Services;

public class RatingService : IRatingService
{
    private readonly IBookService _bookService;
    private readonly IRatingRepository _ratingRepository;

    public RatingService(IBookService bookService, IRatingRepository ratingRepository)
    {
        _bookService = bookService;
        _ratingRepository = ratingRepository;
    }

    public async Task UpsertRatingAsync(string userId, string externalBookId, int score, CancellationToken cancellationToken = default)
    {
        if (score < 1 || score > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Rating must be between 1 and 5.");
        }

        var localBookId = await _bookService.EnsureLocalBookAsync(externalBookId, cancellationToken);
        if (!localBookId.HasValue)
        {
            return;
        }

        await _ratingRepository.UpsertAsync(userId, localBookId.Value, score, cancellationToken);
    }
}
