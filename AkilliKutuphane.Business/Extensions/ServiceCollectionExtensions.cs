using AkilliKutuphane.Business.Options;
using AkilliKutuphane.Business.Services;
using AkilliKutuphane.Business.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AkilliKutuphane.Business.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenLibraryOptions>(configuration.GetSection(OpenLibraryOptions.SectionName));

        services.AddHttpClient<IBookApiClient, OpenLibraryBookApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenLibraryOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IRecommendationService, RecommendationService>();

        return services;
    }
}
