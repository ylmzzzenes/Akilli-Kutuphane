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

        var preferenceTerms = ExtractPreferenceTerms(likedBooks).Take(10).ToList();
        var candidateMap = new Dictionary<string, BookCardDto>(StringComparer.OrdinalIgnoreCase);

        var blendedQueries = BuildBlendedQueries(likedBooks, preferenceTerms);
        foreach (var query in blendedQueries)
        {
            var queryResult = await _bookService.SearchBooksAsync(query, null, 1, 20, userId, cancellationToken);
            foreach (var book in queryResult.Items)
            {
                candidateMap.TryAdd(book.ExternalId, book);
            }
        }

        var excludedExternalIds = likedBooks.Select(x => x.ExternalId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seedTokenMap = likedBooks.ToDictionary(x => x.ExternalId, x => Tokenize($"{x.Title} {x.Authors}"));
        var scored = candidateMap.Values
            .Where(x => !excludedExternalIds.Contains(x.ExternalId))
            .Select(x => ScoreCandidate(x, preferenceTerms, likedBooks, seedTokenMap))
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
            .SelectMany(x => x.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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

    private static AiRecommendationDto ScoreCandidate(
        BookCardDto candidate,
        List<string> preferenceTerms,
        List<BookCardDto> likedBooks,
        Dictionary<string, HashSet<string>> seedTokenMap)
    {
        var text = $"{candidate.Title} {candidate.Authors}".ToLowerInvariant();
        var tokenHits = preferenceTerms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        var tokenScore = Math.Min(tokenHits / 5.0, 1.0);
        var ratingScore = Math.Min(candidate.AverageRating / 5.0, 1.0);
        var recencyScore = candidate.FirstPublishYear.HasValue
            ? Math.Clamp((candidate.FirstPublishYear.Value - 1950) / 100.0, 0.0, 1.0)
            : 0.30;

        var candidateTokens = Tokenize($"{candidate.Title} {candidate.Authors}");
        var seedSimilarities = seedTokenMap.Values
            .Select(seedTokens => ComputeJaccard(candidateTokens, seedTokens))
            .OrderByDescending(x => x)
            .ToList();

        var blendScore = seedSimilarities.Take(Math.Min(3, seedSimilarities.Count)).DefaultIfEmpty(0).Average();
        var coverage = seedSimilarities.Count == 0 ? 0 : seedSimilarities.Count(x => x > 0.05) / (double)seedSimilarities.Count;

        var candidatePrimaryAuthors = ExtractPrimaryAuthors(candidate.Authors);
        var seedPrimaryAuthors = likedBooks.SelectMany(x => ExtractPrimaryAuthors(x.Authors)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameAuthorPenalty = candidatePrimaryAuthors.Any(seedPrimaryAuthors.Contains) ? 0.12 : 0.0;

        var confidence = Math.Clamp(
            (blendScore * 0.50) +
            (coverage * 0.20) +
            (tokenScore * 0.18) +
            (ratingScore * 0.08) +
            (recencyScore * 0.04) -
            sameAuthorPenalty,
            0.0,
            1.0);

        var reason = sameAuthorPenalty > 0
            ? "Favorilerindeki temalara yakın, aynı yazar etkisi azaltıldı"
            : coverage >= 0.5
                ? "Favori kitaplarının karışım temasına benzer"
                : "Favori listenin genel tarzına uyumlu";

        return new AiRecommendationDto
        {
            Book = candidate,
            ConfidenceScore = Math.Round(confidence, 2),
            Reason = reason
        };
    }

    private static IEnumerable<string> BuildBlendedQueries(List<BookCardDto> likedBooks, List<string> preferenceTerms)
    {
        var queries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (preferenceTerms.Count > 0)
        {
            queries.Add(string.Join(' ', preferenceTerms.Take(5)));
            foreach (var chunk in preferenceTerms.Take(9).Chunk(3))
            {
                queries.Add(string.Join(' ', chunk));
            }
        }

        var seedTitleTerms = likedBooks
            .SelectMany(x => x.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(CleanToken)
            .Where(x => x.Length > 2 && !StopWords.Contains(x))
            .Distinct()
            .Take(6)
            .ToList();

        if (seedTitleTerms.Count >= 4)
        {
            queries.Add($"{seedTitleTerms[0]} {seedTitleTerms[2]} {seedTitleTerms[3]}");
            queries.Add($"{seedTitleTerms[1]} {seedTitleTerms[2]} {seedTitleTerms[0]}");
        }

        queries.RemoveWhere(string.IsNullOrWhiteSpace);
        return queries;
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanToken)
            .Where(x => x.Length > 2 && !StopWords.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static double ComputeJaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        var union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> ExtractPrimaryAuthors(string authors)
    {
        return authors
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
