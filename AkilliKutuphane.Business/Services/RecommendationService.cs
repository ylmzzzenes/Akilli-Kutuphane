using System.Text.RegularExpressions;
using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace AkilliKutuphane.Business.Services;

public class RecommendationService : IRecommendationService
{
    private const int MaxRecommendationCount = 24;
    private static readonly TimeSpan RecommendationCacheDuration = TimeSpan.FromMinutes(20);
    private static readonly Regex TokenRegex = new("[^a-zA-Z0-9çğıöşüÇĞİÖŞÜ]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords =
    [
        "ve", "ile", "for", "the", "a", "an", "of", "to", "in", "on", "at", "book", "kitap", "bir", "bu", "that"
    ];

    private readonly IFavoriteService _favoriteService;
    private readonly IBookService _bookService;
    private readonly IRatingRepository _ratingRepository;
    private readonly IMemoryCache _memoryCache;

    public RecommendationService(
        IFavoriteService favoriteService,
        IBookService bookService,
        IRatingRepository ratingRepository,
        IMemoryCache memoryCache)
    {
        _favoriteService = favoriteService;
        _bookService = bookService;
        _ratingRepository = ratingRepository;
        _memoryCache = memoryCache;
    }

    public async Task<IReadOnlyList<AiRecommendationDto>> GetPersonalizedRecommendationsAsync(
        string userId,
        int take = 12,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, MaxRecommendationCount);

        var cacheKey = GetCacheKey(userId);
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<AiRecommendationDto>? cached) && cached is not null)
        {
            return cached.Take(take).ToList();
        }

        var computed = await BuildRecommendationsAsync(userId, MaxRecommendationCount, cancellationToken);
        _memoryCache.Set(cacheKey, computed, RecommendationCacheDuration);
        return computed.Take(take).ToList();
    }

    public Task InvalidateUserRecommendationsAsync(string userId)
    {
        _memoryCache.Remove(GetCacheKey(userId));
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<AiRecommendationDto>> BuildRecommendationsAsync(
        string userId,
        int take,
        CancellationToken cancellationToken)
    {
        var favoriteBooks = await _favoriteService.GetFavoritesAsync(userId, cancellationToken);
        var userRatings = await _ratingRepository.GetUserRatingsMapAsync(userId, cancellationToken);

        var likedBooks = favoriteBooks
            .Concat(favoriteBooks.Where(x => x.LocalBookId.HasValue && userRatings.TryGetValue(x.LocalBookId.Value, out var score) && score >= 4))
            .DistinctBy(x => x.ExternalId)
            .ToList();

        if (!likedBooks.Any())
        {
            var defaultResult = await _bookService.SearchBooksAsync("classic literature", null, 1, take, userId, cancellationToken);
            return defaultResult.Items.Select(x => new AiRecommendationDto
            {
                Book = x,
                ConfidenceScore = 0.45,
                Reason = "Başlangıç önerisi: Popüler klasikler"
            }).ToList();
        }

        var preferenceTerms = ExtractPreferenceTerms(likedBooks).Take(6).ToList();
        var candidateMap = new Dictionary<string, BookCardDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in preferenceTerms)
        {
            var queryResult = await _bookService.SearchBooksAsync(term, null, 1, 20, userId, cancellationToken);
            foreach (var book in queryResult.Items)
            {
                candidateMap.TryAdd(book.ExternalId, book);
            }
        }

        foreach (var book in likedBooks.Take(4))
        {
            var author = book.Authors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(author))
            {
                continue;
            }

            var authorResult = await _bookService.SearchBooksAsync(null, author, 1, 12, userId, cancellationToken);
            foreach (var candidate in authorResult.Items)
            {
                candidateMap.TryAdd(candidate.ExternalId, candidate);
            }
        }

        var excludedExternalIds = likedBooks.Select(x => x.ExternalId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scored = candidateMap.Values
            .Where(x => !excludedExternalIds.Contains(x.ExternalId))
            .Select(x => ScoreCandidate(x, preferenceTerms, likedBooks))
            .OrderByDescending(x => x.ConfidenceScore)
            .ThenByDescending(x => x.Book.AverageRating)
            .Take(take)
            .ToList();

        return scored;
    }

    private static string GetCacheKey(string userId) => $"recommendations:{userId}";

    private static IEnumerable<string> ExtractPreferenceTerms(IEnumerable<BookCardDto> books)
    {
        return books
            .SelectMany(x => $"{x.Title} {x.Authors}".Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(CleanToken)
            .Where(x => x.Length > 2 && !StopWords.Contains(x))
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key);
    }

    private static string CleanToken(string input)
    {
        return TokenRegex.Replace(input, string.Empty).ToLowerInvariant();
    }

    private static AiRecommendationDto ScoreCandidate(BookCardDto candidate, List<string> preferenceTerms, List<BookCardDto> likedBooks)
    {
        var text = $"{candidate.Title} {candidate.Authors}".ToLowerInvariant();
        var tokenHits = preferenceTerms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        var tokenScore = Math.Min(tokenHits / 5.0, 1.0);
        var ratingScore = Math.Min(candidate.AverageRating / 5.0, 1.0);
        var recencyScore = candidate.FirstPublishYear.HasValue
            ? Math.Clamp((candidate.FirstPublishYear.Value - 1950) / 100.0, 0.0, 1.0)
            : 0.35;

        var authorBoost = likedBooks.Any(seed =>
            candidate.Authors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(author => seed.Authors.Contains(author, StringComparison.OrdinalIgnoreCase)))
            ? 0.15
            : 0.0;

        var confidence = Math.Clamp((tokenScore * 0.45) + (ratingScore * 0.35) + (recencyScore * 0.2) + authorBoost, 0.0, 1.0);
        var reason = authorBoost > 0
            ? "Sevdiğin yazarlara benzer"
            : tokenHits > 0
                ? "İlgi alanınla eşleşen tema/yazar"
                : "Genel puanı yüksek ve uyumlu kitap";

        return new AiRecommendationDto
        {
            Book = candidate,
            ConfidenceScore = Math.Round(confidence, 2),
            Reason = reason
        };
    }
}
