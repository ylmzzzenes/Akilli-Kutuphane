namespace AkilliKutuphane.Business.Options;

public class OpenLibraryOptions
{
    public const string SectionName = "OpenLibrary";
    public string BaseUrl { get; set; } = "https://openlibrary.org";
    public int CacheMinutes { get; set; } = 10;
}
