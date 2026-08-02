namespace BuildingBlocks.DataAccess.Abstractions;

public interface IDbConnectionFactory
{
    System.Data.IDbConnection CreateConnection(string name = "Owned");
}

public interface IDataAccessStrategy<TSummary, TUpdate>
{
    Task<TSummary?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(TUpdate update, CancellationToken cancellationToken = default);
}
