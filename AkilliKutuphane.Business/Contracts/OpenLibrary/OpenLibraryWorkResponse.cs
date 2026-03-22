using System.Text.Json;
using System.Text.Json.Serialization;

namespace AkilliKutuphane.Business.Contracts.OpenLibrary;

public class OpenLibraryWorkResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public JsonElement? Description { get; set; }

    [JsonPropertyName("covers")]
    public List<int>? Covers { get; set; }
}
