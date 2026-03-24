using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace AkilliKutuphane.Business.Services;

public class BookService : IBookService
{
    private static readonly IReadOnlyList<Book> StarterCatalog =
    [
        new Book
        {
            ExternalId = "seed-dune",
            Title = "Dune",
            Authors = "Frank Herbert",
            Description = "Arrakis gezegeninde geçen, siyaset ve ekoloji odaklı bilim kurgu klasiği.",
            FirstPublishYear = 1965,
            CoverImageUrl = "https://placehold.co/600x400/e9ecef/6c757d?text=Dune"
        },
        new Book
        {
            ExternalId = "seed-hobbit",
            Title = "The Hobbit",
            Authors = "J.R.R. Tolkien",
            Description = "Orta Dünya'da bir macera yolculuğunu anlatan modern fantastik klasik.",
            FirstPublishYear = 1937,
            CoverImageUrl = "https://placehold.co/600x400/e9ecef/6c757d?text=The+Hobbit"
        },
        new Book
        {
            ExternalId = "seed-1984",
            Title = "1984",
            Authors = "George Orwell",
            Description = "Gözetim toplumu ve totaliter düzen üzerine distopik bir başyapıt.",
            FirstPublishYear = 1949,
            CoverImageUrl = "https://placehold.co/600x400/e9ecef/6c757d?text=1984"
        },
        new Book
        {
            ExternalId = "seed-sherlock",
            Title = "Sherlock Holmes: The Hound of the Baskervilles",
            Authors = "Arthur Conan Doyle",
            Description = "Dedektiflik türünün en bilinen vakalarından biri.",
            FirstPublishYear = 1902,
            CoverImageUrl = "https://placehold.co/600x400/e9ecef/6c757d?text=Sherlock+Holmes"
        },
        new Book
        {
            ExternalId = "seed-lean-startup",
            Title = "The Lean Startup",
            Authors = "Eric Ries",
            Description = "Ürün geliştirme ve deney odaklı girişim yaklaşımını anlatır.",
            FirstPublishYear = 2011,
            CoverImageUrl = "https://placehold.co/600x400/e9ecef/6c757d?text=Lean+Startup"
        }
    ];

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

        if (!result.Items.Any() && string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(author))
        {
            _logger.LogWarning("OpenLibrary returned no books for default browse. Falling back to local catalog.");
            result = await BuildCatalogFallbackAsync(page, pageSize, userId, cancellationToken);
        }

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
        var existingLocal = await _bookRepository.GetByExternalIdAsync(externalId, cancellationToken);
        if (existingLocal is not null)
        {
            var localDetail = new BookDetailDto
            {
                LocalBookId = existingLocal.Id,
                ExternalId = existingLocal.ExternalId,
                Title = existingLocal.Title,
                Authors = existingLocal.Authors,
                Description = existingLocal.Description,
                CoverImageUrl = existingLocal.CoverImageUrl,
                FirstPublishYear = existingLocal.FirstPublishYear
            };

            var localAverages = await _ratingRepository.GetAverageScoresAsync(new[] { existingLocal.Id }, cancellationToken);
            localDetail.AverageRating = localAverages.GetValueOrDefault(existingLocal.Id);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                localDetail.IsFavorite = await _favoriteRepository.IsFavoriteAsync(userId, existingLocal.Id, cancellationToken);
                var localRating = await _ratingRepository.GetUserRatingAsync(userId, existingLocal.Id, cancellationToken);
                localDetail.UserRating = localRating?.Score;
            }

            return localDetail;
        }

        var detail = await _bookApiClient.GetBookDetailAsync(externalId, cancellationToken);
        if (detail is null)
        {
            var localFallback = await _bookRepository.GetByExternalIdAsync(externalId, cancellationToken);
            if (localFallback is null)
            {
                return null;
            }

            detail = new BookDetailDto
            {
                LocalBookId = localFallback.Id,
                ExternalId = localFallback.ExternalId,
                Title = localFallback.Title,
                Authors = localFallback.Authors,
                Description = localFallback.Description,
                CoverImageUrl = localFallback.CoverImageUrl,
                FirstPublishYear = localFallback.FirstPublishYear
            };
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

    private async Task<PagedResultDto<BookCardDto>> BuildCatalogFallbackAsync(
        int page,
        int pageSize,
        string? userId,
        CancellationToken cancellationToken)
    {
        var take = Math.Max(page * pageSize, pageSize);
        var localBooks = await _bookRepository.GetCatalogExcludingIdsAsync(Array.Empty<int>(), take, cancellationToken);
        if (!localBooks.Any())
        {
            await EnsureStarterCatalogAsync(cancellationToken);
            localBooks = await _bookRepository.GetCatalogExcludingIdsAsync(Array.Empty<int>(), take, cancellationToken);
        }

        var mappedAll = localBooks
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new BookCardDto
            {
                LocalBookId = x.Id,
                ExternalId = x.ExternalId,
                Title = x.Title,
                Authors = x.Authors,
                CoverImageUrl = x.CoverImageUrl,
                FirstPublishYear = x.FirstPublishYear
            })
            .ToList();

        var items = mappedAll
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        await EnrichWithRatingsAndFavoritesAsync(items, userId, cancellationToken);

        return new PagedResultDto<BookCardDto>
        {
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = mappedAll.Count,
            Items = items
        };
    }

    private async Task EnsureStarterCatalogAsync(CancellationToken cancellationToken)
    {
        foreach (var seed in StarterCatalog)
        {
            await _bookRepository.UpsertExternalBookAsync(new Book
            {
                ExternalId = seed.ExternalId,
                Title = seed.Title,
                Authors = seed.Authors,
                Description = seed.Description,
                CoverImageUrl = seed.CoverImageUrl,
                FirstPublishYear = seed.FirstPublishYear
            }, cancellationToken);
        }
    }
}
