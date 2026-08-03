using BuildingBlocks.DataAccess.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Domain;

namespace ShowcaseDataService.Infrastructure;

public sealed class ShowcaseDbContext : DbContext
{
    private readonly string _schema;

    public ShowcaseDbContext(DbContextOptions<ShowcaseDbContext> options)
        : this(options, Microsoft.Extensions.Options.Options.Create(new ShowcaseDatabaseOptions()))
    {
    }

    public ShowcaseDbContext(
        DbContextOptions<ShowcaseDbContext> options,
        IOptions<ShowcaseDatabaseOptions> database)
        : base(options)
    {
        _schema = database.Value.NormalizedSchema;
    }

    public string Schema => _schema;

    public DbSet<ShowcaseItem> Items => Set<ShowcaseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        modelBuilder.Entity<ShowcaseItem>(e =>
        {
            e.ToTable(ShowcaseDatabaseOptions.ItemsTableName);
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
        });
    }
}
