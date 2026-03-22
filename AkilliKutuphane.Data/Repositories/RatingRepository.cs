using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Data.Persistence;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AkilliKutuphane.Data.Repositories;

public class RatingRepository : IRatingRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RatingRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Rating?> GetUserRatingAsync(string userId, int bookId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Ratings.FirstOrDefaultAsync(x => x.UserId == userId && x.BookId == bookId, cancellationToken);
    }

    public async Task UpsertAsync(string userId, int bookId, int score, CancellationToken cancellationToken = default)
    {
        var rating = await GetUserRatingAsync(userId, bookId, cancellationToken);
        if (rating is null)
        {
            _dbContext.Ratings.Add(new Rating
            {
                UserId = userId,
                BookId = bookId,
                Score = score,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            rating.Score = score;
            rating.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<int, double>> GetAverageScoresAsync(IEnumerable<int> bookIds, CancellationToken cancellationToken = default)
    {
        var idList = bookIds.Distinct().ToList();
        return await _dbContext.Ratings
            .Where(x => idList.Contains(x.BookId))
            .GroupBy(x => x.BookId)
            .Select(x => new { BookId = x.Key, Average = x.Average(v => v.Score) })
            .ToDictionaryAsync(x => x.BookId, x => x.Average, cancellationToken);
    }

    public Task<Dictionary<int, int>> GetUserRatingsMapAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Ratings
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.BookId, x => x.Score, cancellationToken);
    }

    public async Task<IReadOnlyList<Rating>> GetUserRatingsDetailedAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Ratings
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
