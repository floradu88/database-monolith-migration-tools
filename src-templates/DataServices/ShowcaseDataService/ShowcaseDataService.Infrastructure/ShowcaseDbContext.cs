using BuildingBlocks.DataAccess.EfCore;
using Microsoft.EntityFrameworkCore;
using ShowcaseDataService.Domain;

namespace ShowcaseDataService.Infrastructure;

public sealed class ShowcaseDbContext : DbContext
{
    public ShowcaseDbContext(DbContextOptions<ShowcaseDbContext> options) : base(options) { }

    public DbSet<ShowcaseItem> Items => Set<ShowcaseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("showcase");
        modelBuilder.Entity<ShowcaseItem>(e =>
        {
            e.ToTable("Items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
        });
    }
}
