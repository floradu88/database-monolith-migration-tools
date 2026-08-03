using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.DataAccess.EfCore;

public static class EfCoreConventions
{
    public const string MigrationHistorySchema = "deployment";
    public const string MigrationHistoryTable = "__EFMigrationsHistory";

    public static DbContextOptionsBuilder UseShowcaseSqlServer(
        this DbContextOptionsBuilder builder,
        string connectionString)
    {
        return builder.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable(MigrationHistoryTable, MigrationHistorySchema));
    }

    public static DbContextOptionsBuilder<TContext> UseShowcaseSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DbContext
    {
        UseShowcaseSqlServer((DbContextOptionsBuilder)builder, connectionString);
        return builder;
    }
}
