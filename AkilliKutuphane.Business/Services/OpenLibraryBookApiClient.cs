using System.Text.Json;
using AkilliKutuphane.Business.Contracts.OpenLibrary;
using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Options;
using AkilliKutuphane.Business.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AkilliKutuphane.Business.Services;

public class OpenLibraryBookApiClient : IBookApiClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OpenLibraryBookApiClient> _logger;
    private readonly OpenLibraryOptions _options;

    public OpenLibraryBookApiClient(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        IOptions<OpenLibraryOptions> options,
        ILogger<OpenLibraryBookApiClient> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<PagedResultDto<BookCardDto>> SearchBooksAsync(string? query, string? author, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 40);

        var cacheKey = $"openlibrary:search:{query}:{author}:{page}:{pageSize}";
        if (_memoryCache.TryGetValue(cacheKey, out PagedResultDto<BookCardDto>? cachedResult) && cachedResult is not null)
        {
            return cachedResult;
        }

        var q = Uri.EscapeDataString(query ?? string.Empty);
        var a = Uri.EscapeDataString(author ?? string.Empty);
        var requestUrl = $"/search.json?q={q}&author={a}&page={page}&limit={pageSize}";

        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenLibrary search request failed with status code {StatusCode}", response.StatusCode);
            return new PagedResultDto<BookCardDto>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OpenLibrarySearchResponse>(stream, JsonSerializerOptions, cancellationToken);
        if (payload is null)
        {
            return new PagedResultDto<BookCardDto>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        var items = payload.Docs.Select(MapSearchDoc).ToList();
        var result = new PagedResultDto<BookCardDto>
        {
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = payload.NumFound,
            Items = items
        };

        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));
        return result;
    }

    public async Task<BookDetailDto?> GetBookDetailAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"openlibrary:detail:{externalId}";
        if (_memoryCache.TryGetValue(cacheKey, out BookDetailDto? cachedResult) && cachedResult is not null)
        {
            return cachedResult;
        }

        var search = await SearchBooksAsync(externalId, null, 1, 1, cancellationToken);
        var matched = search.Items.FirstOrDefault(x => string.Equals(x.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return null;
        }

        string? description = null;
        try
        {
            var workResponse = await _httpClient.GetAsync($"/works/{externalId}.json", cancellationToken);
            if (workResponse.IsSuccessStatusCode)
            {
                await using var stream = await workResponse.Content.ReadAsStreamAsync(cancellationToken);
                var workPayload = await JsonSerializer.DeserializeAsync<OpenLibraryWorkResponse>(stream, JsonSerializerOptions, cancellationToken);
                description = workPayload?.Description?.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenLibrary detail request failed for {ExternalId}", externalId);
        }

        var result = new BookDetailDto
        {
            ExternalId = matched.ExternalId,
            Title = matched.Title,
            Authors = matched.Authors,
            CoverImageUrl = matched.CoverImageUrl,
            FirstPublishYear = matched.FirstPublishYear,
            Description = description
        };

        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));
        return result;
    }

    private static BookCardDto MapSearchDoc(OpenLibraryBookDoc doc)
    {
        var externalId = doc.Key.Replace("/works/", string.Empty, StringComparison.OrdinalIgnoreCase);
        return new BookCardDto
        {
            ExternalId = externalId,
            Title = doc.Title,
            Authors = string.Join(", ", doc.AuthorNames ?? new List<string> { "Unknown" }),
            FirstPublishYear = doc.FirstPublishYear,
            CoverImageUrl = doc.CoverId.HasValue
                ? $"https://covers.openlibrary.org/b/id/{doc.CoverId.Value}-L.jpg"
                : null
        };
    }
}
