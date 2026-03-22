using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace AkilliKutuphane.Business.Services;

public class BookService : IBookService
{
    private readonly IBookApiClient _bookApiClient;
    private readonly IBookRepository _bookRepository;
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IRatingRepository _ratingRepository;
    private readonly ILogger<BookService> _logger;

    public BookService(
        IBookApiClient bookApiClient,
        IBookRepository bookRepository,
        IFavoriteRepository favoriteRepository,
        IRatingRepository ratingRepository,
        ILogger<BookService> logger)
    {
        _bookApiClient = bookApiClient;
        _bookRepository = bookRepository;
        _favoriteRepository = favoriteRepository;
        _ratingRepository = ratingRepository;
        _logger = logger;
    }

    public async Task<PagedResultDto<BookCardDto>> SearchBooksAsync(
        string? query,
        string? author,
        int page,
        int pageSize,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _bookApiClient.SearchBooksAsync(query, author, page, pageSize, cancellationToken);
        foreach (var item in result.Items)
        {
            var local = await _bookRepository.UpsertExternalBookAsync(MapToBookEntity(item), cancellationToken);
            item.LocalBookId = local.Id;
        }

        await EnrichWithRatingsAndFavoritesAsync(result.Items, userId, cancellationToken);
        return result;
    }

    public async Task<BookDetailDto?> GetBookDetailAsync(string externalId, string? userId, CancellationToken cancellationToken = default)
    {
        var detail = await _bookApiClient.GetBookDetailAsync(externalId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var local = await _bookRepository.UpsertExternalBookAsync(MapToBookEntity(detail), cancellationToken);
        detail.LocalBookId = local.Id;

        var averages = await _ratingRepository.GetAverageScoresAsync(new[] { local.Id }, cancellationToken);
        detail.AverageRating = averages.GetValueOrDefault(local.Id);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            detail.IsFavorite = await _favoriteRepository.IsFavoriteAsync(userId, local.Id, cancellationToken);
            var rating = await _ratingRepository.GetUserRatingAsync(userId, local.Id, cancellationToken);
            detail.UserRating = rating?.Score;
        }

        return detail;
    }

    public async Task<int?> EnsureLocalBookAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var existing = await _bookRepository.GetByExternalIdAsync(externalId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var detail = await _bookApiClient.GetBookDetailAsync(externalId, cancellationToken);
        if (detail is null)
        {
            _logger.LogWarning("Unable to synchronize local book for external id {ExternalId}", externalId);
            return null;
        }

        var local = await _bookRepository.UpsertExternalBookAsync(MapToBookEntity(detail), cancellationToken);
        return local.Id;
    }

    private async Task EnrichWithRatingsAndFavoritesAsync(
        IReadOnlyList<BookCardDto> books,
        string? userId,
        CancellationToken cancellationToken)
    {
        var localBookIds = books
            .Where(x => x.LocalBookId.HasValue)
            .Select(x => x.LocalBookId!.Value)
            .Distinct()
            .ToList();

        var averages = await _ratingRepository.GetAverageScoresAsync(localBookIds, cancellationToken);
        foreach (var book in books.Where(x => x.LocalBookId.HasValue))
        {
            book.AverageRating = averages.GetValueOrDefault(book.LocalBookId!.Value);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var favoriteIds = await _favoriteRepository.GetFavoriteBookIdsAsync(userId, cancellationToken);
        var favoriteSet = favoriteIds.ToHashSet();
        foreach (var book in books.Where(x => x.LocalBookId.HasValue))
        {
            book.IsFavorite = favoriteSet.Contains(book.LocalBookId!.Value);
        }
    }

    private static Book MapToBookEntity(BookCardDto dto)
    {
        return new Book
        {
            ExternalId = dto.ExternalId,
            Title = dto.Title,
            Authors = dto.Authors,
            CoverImageUrl = dto.CoverImageUrl,
            FirstPublishYear = dto.FirstPublishYear
        };
    }

    private static Book MapToBookEntity(BookDetailDto dto)
    {
        return new Book
        {
            ExternalId = dto.ExternalId,
            Title = dto.Title,
            Authors = dto.Authors,
            Description = dto.Description,
            CoverImageUrl = dto.CoverImageUrl,
            FirstPublishYear = dto.FirstPublishYear
        };
    }
}
