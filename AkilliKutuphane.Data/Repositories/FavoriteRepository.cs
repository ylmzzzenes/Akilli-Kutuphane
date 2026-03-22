using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Data.Persistence;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AkilliKutuphane.Data.Repositories;

public class FavoriteRepository : IFavoriteRepository
{
    private readonly ApplicationDbContext _dbContext;

    public FavoriteRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsFavoriteAsync(string userId, int bookId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Favorites.AnyAsync(x => x.UserId == userId && x.BookId == bookId, cancellationToken);
    }

    public async Task AddAsync(string userId, int bookId, CancellationToken cancellationToken = default)
    {
        var alreadyExists = await IsFavoriteAsync(userId, bookId, cancellationToken);
        if (alreadyExists)
        {
            return;
        }

        _dbContext.Favorites.Add(new Favorite
        {
            UserId = userId,
            BookId = bookId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string userId, int bookId, CancellationToken cancellationToken = default)
    {
        var favorite = await _dbContext.Favorites.FirstOrDefaultAsync(
            x => x.UserId == userId && x.BookId == bookId,
            cancellationToken);

        if (favorite is null)
        {
            return;
        }

        _dbContext.Favorites.Remove(favorite);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetFavoriteBookIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Favorites
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.BookId)
            .ToListAsync(cancellationToken);
    }
}
