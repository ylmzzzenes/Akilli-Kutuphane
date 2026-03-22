using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Data.Persistence;
using AkilliKutuphane.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AkilliKutuphane.Data.Repositories;

public class BookRepository : IBookRepository
{
    private const int TitleMaxLength = 300;
    private const int AuthorsMaxLength = 500;
    private readonly ApplicationDbContext _dbContext;

    public BookRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Book?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Books.FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);
    }

    public Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Books.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Book>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var list = ids.Distinct().ToList();
        return await _dbContext.Books.Where(x => list.Contains(x.Id)).ToListAsync(cancellationToken);
    }

    public async Task<Book> UpsertExternalBookAsync(Book input, CancellationToken cancellationToken = default)
    {
        input.Title = Truncate(input.Title, TitleMaxLength);
        input.Authors = Truncate(input.Authors, AuthorsMaxLength);

        var existing = await _dbContext.Books.FirstOrDefaultAsync(x => x.ExternalId == input.ExternalId, cancellationToken);
        if (existing is null)
        {
            _dbContext.Books.Add(input);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return input;
        }

        existing.Title = input.Title;
        existing.Authors = input.Authors;
        existing.Description = input.Description;
        existing.CoverImageUrl = input.CoverImageUrl;
        existing.FirstPublishYear = input.FirstPublishYear;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
