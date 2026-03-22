using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Repositories.Interfaces;

namespace AkilliKutuphane.Business.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IBookService _bookService;
    private readonly IBookRepository _bookRepository;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IRatingRepository _ratingRepository;

    public FavoriteService(
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

    public async Task AddFavoriteAsync(string userId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var localBookId = await _bookService.EnsureLocalBookAsync(externalBookId, cancellationToken);
        if (!localBookId.HasValue)
        {
            return;
        }

        await _favoriteRepository.AddAsync(userId, localBookId.Value, cancellationToken);
    }

    public async Task RemoveFavoriteAsync(string userId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var localBook = await _bookRepository.GetByExternalIdAsync(externalBookId, cancellationToken);
        if (localBook is null)
        {
            return;
        }

        await _favoriteRepository.RemoveAsync(userId, localBook.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<BookCardDto>> GetFavoritesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var bookIds = await _favoriteRepository.GetFavoriteBookIdsAsync(userId, cancellationToken);
        var books = await _bookRepository.GetByIdsAsync(bookIds, cancellationToken);
        var averages = await _ratingRepository.GetAverageScoresAsync(bookIds, cancellationToken);

        return books.Select(book => new BookCardDto
        {
            LocalBookId = book.Id,
            ExternalId = book.ExternalId,
            Title = book.Title,
            Authors = book.Authors,
            CoverImageUrl = book.CoverImageUrl,
            FirstPublishYear = book.FirstPublishYear,
            IsFavorite = true,
            AverageRating = averages.GetValueOrDefault(book.Id)
        }).ToList();
    }
}
