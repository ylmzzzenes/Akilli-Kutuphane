using AkilliKutuphane.Data.Repositories;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AkilliKutuphane.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccessServices(this IServiceCollection services)
    {
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        return services;
    }
}
