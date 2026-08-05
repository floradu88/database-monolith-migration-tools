using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.DataAccess.EfCore;

public static class EfCoreConventions
{
    public const string MigrationHistorySchema = "deployment";
    public const string MigrationHistoryTable = "__EFMigrationsHistory";

    public static DbContextOptionsBuilder UseShowcaseSqlServer(
        this DbContextOptionsBuilder builder,
        string connectionString,
        string? migrationsAssemblyName = null)
    {
        return builder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable(MigrationHistoryTable, MigrationHistorySchema);
            if (!string.IsNullOrWhiteSpace(migrationsAssemblyName))
                sql.MigrationsAssembly(migrationsAssemblyName);
        });
    }

    public static DbContextOptionsBuilder<TContext> UseShowcaseSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        string connectionString,
        string? migrationsAssemblyName = null)
        where TContext : DbContext
    {
        UseShowcaseSqlServer((DbContextOptionsBuilder)builder, connectionString, migrationsAssemblyName);
        return builder;
    }
}
