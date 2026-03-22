using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Repositories.Interfaces;

namespace AkilliKutuphane.Business.Services;

public class RatingService : IRatingService
{
    private readonly IBookService _bookService;
    private readonly IBookRepository _bookRepository;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IRatingRepository _ratingRepository;

    public RatingService(
        IBookService bookService,
        IBookRepository bookRepository,
        IFavoriteRepository favoriteRepository,
        IRatingRepository ratingRepository)
    {
        _bookService = bookService;
        _bookRepository = bookRepository;
        _favoriteRepository = favoriteRepository;
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

    public async Task<IReadOnlyList<RatedBookDto>> GetRatedBooksAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ratings = await _ratingRepository.GetUserRatingsDetailedAsync(userId, cancellationToken);
        var bookIds = ratings.Select(x => x.BookId).Distinct().ToList();
        if (bookIds.Count == 0)
        {
            return Array.Empty<RatedBookDto>();
        }

        var books = await _bookRepository.GetByIdsAsync(bookIds, cancellationToken);
        var averages = await _ratingRepository.GetAverageScoresAsync(bookIds, cancellationToken);
        var favoriteIds = await _favoriteRepository.GetFavoriteBookIdsAsync(userId, cancellationToken);
        var favoriteSet = favoriteIds.ToHashSet();

        var bookMap = books.ToDictionary(x => x.Id);
        return ratings
            .Where(r => bookMap.ContainsKey(r.BookId))
            .Select(r =>
            {
                var book = bookMap[r.BookId];
                return new RatedBookDto
                {
                    UserScore = r.Score,
                    UpdatedAtUtc = r.UpdatedAtUtc,
                    Book = new BookCardDto
                    {
                        LocalBookId = book.Id,
                        ExternalId = book.ExternalId,
                        Title = book.Title,
                        Authors = book.Authors,
                        CoverImageUrl = book.CoverImageUrl,
                        FirstPublishYear = book.FirstPublishYear,
                        AverageRating = averages.GetValueOrDefault(book.Id),
                        IsFavorite = favoriteSet.Contains(book.Id)
                    }
                };
            })
            .ToList();
    }
}
