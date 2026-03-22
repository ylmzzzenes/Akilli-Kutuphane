using System.Text.Json.Serialization;

namespace AkilliKutuphane.Business.Contracts.OpenLibrary;

public class OpenLibrarySearchResponse
{
    [JsonPropertyName("numFound")]
    public int NumFound { get; set; }

    [JsonPropertyName("docs")]
    public List<OpenLibraryBookDoc> Docs { get; set; } = new();
}

public class OpenLibraryBookDoc
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author_name")]
    public List<string>? AuthorNames { get; set; }

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; set; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; set; }
}
