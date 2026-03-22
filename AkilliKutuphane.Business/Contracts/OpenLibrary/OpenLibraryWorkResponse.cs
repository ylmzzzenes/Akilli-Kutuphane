using System.Text.Json.Serialization;

namespace AkilliKutuphane.Business.Contracts.OpenLibrary;

public class OpenLibraryWorkResponse
{
    [JsonPropertyName("description")]
    public JsonElementWrapper? Description { get; set; }
}

public class JsonElementWrapper
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
