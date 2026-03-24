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

        try
        {
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenLibrary search request failed with status code {StatusCode}", response.StatusCode);
                return await SearchGoogleBooksAsync(query, author, page, pageSize, cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OpenLibrarySearchResponse>(stream, JsonSerializerOptions, cancellationToken);
            if (payload is null)
            {
                return await SearchGoogleBooksAsync(query, author, page, pageSize, cancellationToken);
            }

            var items = payload.Docs.Select(MapSearchDoc).ToList();
            if (items.Count == 0)
            {
                return await SearchGoogleBooksAsync(query, author, page, pageSize, cancellationToken);
            }

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
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "OpenLibrary search request timed out for query '{Query}' and author '{Author}'", query, author);
            return await SearchGoogleBooksAsync(query, author, page, pageSize, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenLibrary search request failed for query '{Query}' and author '{Author}'", query, author);
            return await SearchGoogleBooksAsync(query, author, page, pageSize, cancellationToken);
        }
    }

    public async Task<BookDetailDto?> GetBookDetailAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (externalId.StartsWith("gb:", StringComparison.OrdinalIgnoreCase))
        {
            return await GetGoogleBookDetailAsync(externalId, cancellationToken);
        }

        var cacheKey = $"openlibrary:detail:{externalId}";
        if (_memoryCache.TryGetValue(cacheKey, out BookDetailDto? cachedResult) && cachedResult is not null)
        {
            return cachedResult;
        }

        BookCardDto? matched = null;
        string? description = null;
        string? titleFromWork = null;
        string? coverFromWork = null;
        try
        {
            var workResponse = await _httpClient.GetAsync($"/works/{externalId}.json", cancellationToken);
            if (workResponse.IsSuccessStatusCode)
            {
                await using var stream = await workResponse.Content.ReadAsStreamAsync(cancellationToken);
                var workPayload = await JsonSerializer.DeserializeAsync<OpenLibraryWorkResponse>(stream, JsonSerializerOptions, cancellationToken);
                description = ParseDescription(workPayload?.Description);
                titleFromWork = workPayload?.Title;

                var coverId = workPayload?.Covers?.FirstOrDefault();
                if (coverId.HasValue)
                {
                    coverFromWork = $"https://covers.openlibrary.org/b/id/{coverId.Value}-L.jpg";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenLibrary detail request failed for {ExternalId}", externalId);
        }

        var search = await SearchBooksAsync(externalId, null, 1, 1, cancellationToken);
        matched = search.Items.FirstOrDefault(x => string.Equals(x.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));

        if (matched is null && !string.IsNullOrWhiteSpace(titleFromWork))
        {
            var fallbackSearch = await SearchBooksAsync(titleFromWork, null, 1, 1, cancellationToken);
            matched = fallbackSearch.Items.FirstOrDefault();
        }

        if (matched is null && string.IsNullOrWhiteSpace(titleFromWork))
        {
            return null;
        }

        var result = new BookDetailDto
        {
            ExternalId = matched?.ExternalId ?? externalId,
            Title = matched?.Title ?? titleFromWork ?? "Bilinmeyen Kitap",
            Authors = matched?.Authors ?? "Bilinmiyor",
            CoverImageUrl = matched?.CoverImageUrl ?? coverFromWork,
            FirstPublishYear = matched?.FirstPublishYear,
            Description = description
        };

        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));
        return result;
    }

    private static string? ParseDescription(JsonElement? description)
    {
        if (!description.HasValue)
        {
            return null;
        }

        var value = description.Value;
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var innerValue))
        {
            return innerValue.GetString();
        }

        return null;
    }

    private static BookCardDto MapSearchDoc(OpenLibraryBookDoc doc)
    {
        var externalId = doc.Key.Replace("/works/", string.Empty, StringComparison.OrdinalIgnoreCase);
        return new BookCardDto
        {
            ExternalId = externalId,
            Title = doc.Title,
            Authors = string.Join(", ", doc.AuthorNames ?? new List<string> { "Bilinmiyor" }),
            FirstPublishYear = doc.FirstPublishYear,
            CoverImageUrl = doc.CoverId.HasValue
                ? $"https://covers.openlibrary.org/b/id/{doc.CoverId.Value}-L.jpg"
                : null
        };
    }

    private static PagedResultDto<BookCardDto> BuildEmptyResult(int page, int pageSize) =>
        new()
        {
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = 0
        };

    private async Task<PagedResultDto<BookCardDto>> SearchGoogleBooksAsync(
        string? query,
        string? author,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(author))
        {
            return BuildEmptyResult(page, pageSize);
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            parts.Add(query.Trim());
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add($"inauthor:{author.Trim()}");
        }

        var search = Uri.EscapeDataString(string.Join(' ', parts));
        var startIndex = Math.Max((page - 1) * pageSize, 0);
        var url = $"https://www.googleapis.com/books/v1/volumes?q={search}&startIndex={startIndex}&maxResults={pageSize}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Books search request failed with status code {StatusCode}", response.StatusCode);
                return BuildEmptyResult(page, pageSize);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var totalCount = root.TryGetProperty("totalItems", out var totalItemsElement)
                && totalItemsElement.ValueKind == JsonValueKind.Number
                ? totalItemsElement.GetInt32()
                : 0;

            var items = new List<BookCardDto>();
            if (root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var mapped = MapGoogleBook(item);
                    if (mapped is not null)
                    {
                        items.Add(mapped);
                    }
                }
            }

            return new PagedResultDto<BookCardDto>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = Math.Max(totalCount, items.Count),
                Items = items
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Books search request failed for query '{Query}' and author '{Author}'", query, author);
            return BuildEmptyResult(page, pageSize);
        }
    }

    private async Task<BookDetailDto?> GetGoogleBookDetailAsync(string externalId, CancellationToken cancellationToken)
    {
        var cacheKey = $"googlebooks:detail:{externalId}";
        if (_memoryCache.TryGetValue(cacheKey, out BookDetailDto? cached) && cached is not null)
        {
            return cached;
        }

        var volumeId = externalId["gb:".Length..];
        if (string.IsNullOrWhiteSpace(volumeId))
        {
            return null;
        }

        try
        {
            var url = $"https://www.googleapis.com/books/v1/volumes/{Uri.EscapeDataString(volumeId)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var card = MapGoogleBook(document.RootElement);
            if (card is null)
            {
                return null;
            }

            string? description = null;
            if (document.RootElement.TryGetProperty("volumeInfo", out var volumeInfo)
                && volumeInfo.TryGetProperty("description", out var descriptionElement)
                && descriptionElement.ValueKind == JsonValueKind.String)
            {
                description = descriptionElement.GetString();
            }

            var detail = new BookDetailDto
            {
                ExternalId = card.ExternalId,
                Title = card.Title,
                Authors = card.Authors,
                CoverImageUrl = card.CoverImageUrl,
                FirstPublishYear = card.FirstPublishYear,
                Description = description
            };

            _memoryCache.Set(cacheKey, detail, TimeSpan.FromMinutes(_options.CacheMinutes));
            return detail;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Books detail request failed for {ExternalId}", externalId);
            return null;
        }
    }

    private static BookCardDto? MapGoogleBook(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var id = idElement.GetString();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (!item.TryGetProperty("volumeInfo", out var volumeInfo) || volumeInfo.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var title = volumeInfo.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
            ? titleElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var authors = "Bilinmiyor";
        if (volumeInfo.TryGetProperty("authors", out var authorsElement) && authorsElement.ValueKind == JsonValueKind.Array)
        {
            var authorList = authorsElement.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (authorList.Count > 0)
            {
                authors = string.Join(", ", authorList);
            }
        }

        int? firstPublishYear = null;
        if (volumeInfo.TryGetProperty("publishedDate", out var publishedDateElement)
            && publishedDateElement.ValueKind == JsonValueKind.String)
        {
            var publishedDate = publishedDateElement.GetString();
            if (!string.IsNullOrWhiteSpace(publishedDate) && publishedDate.Length >= 4
                && int.TryParse(publishedDate[..4], out var year))
            {
                firstPublishYear = year;
            }
        }

        string? coverImage = null;
        if (volumeInfo.TryGetProperty("imageLinks", out var imageLinksElement)
            && imageLinksElement.ValueKind == JsonValueKind.Object
            && imageLinksElement.TryGetProperty("thumbnail", out var thumbnailElement)
            && thumbnailElement.ValueKind == JsonValueKind.String)
        {
            coverImage = thumbnailElement.GetString();
            if (!string.IsNullOrWhiteSpace(coverImage))
            {
                coverImage = coverImage.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);
            }
        }

        return new BookCardDto
        {
            ExternalId = $"gb:{id}",
            Title = title,
            Authors = authors,
            FirstPublishYear = firstPublishYear,
            CoverImageUrl = coverImage
        };
    }
}
