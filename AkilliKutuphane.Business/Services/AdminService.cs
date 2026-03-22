using AkilliKutuphane.Business.Models;
using AkilliKutuphane.Business.Services.Interfaces;
using AkilliKutuphane.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AkilliKutuphane.Business.Services;

public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        return new AdminDashboardDto
        {
            TotalUsers = await _dbContext.Users.CountAsync(cancellationToken),
            TotalBooks = await _dbContext.Books.CountAsync(cancellationToken),
            TotalFavorites = await _dbContext.Favorites.CountAsync(cancellationToken),
            TotalRatings = await _dbContext.Ratings.CountAsync(cancellationToken)
        };
    }
}
