using AkilliKutuphane.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AkilliKutuphane.Data.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Rating> Ratings => Set<Rating>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Book>(entity =>
        {
            entity.HasIndex(x => x.ExternalId).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Authors).HasMaxLength(500).IsRequired();
        });

        builder.Entity<Favorite>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.Favorites)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Book)
                .WithMany(x => x.Favorites)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Rating>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
            entity.Property(x => x.Score).IsRequired();
            entity.ToTable(x => x.HasCheckConstraint("CK_Ratings_Score_Range", "[Score] >= 1 AND [Score] <= 5"));
            entity.HasOne(x => x.User)
                .WithMany(x => x.Ratings)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Book)
                .WithMany(x => x.Ratings)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
