using BuildingBlocks.Migration;
using Xunit;

namespace Reconciliation.Tests;

public class TableIntegrityComparerTests
{
    [Fact]
    public void Matching_delta_rows_are_equal()
    {
        var cols = new[] { "ExternalId", "Name", "Status" };
        var dbo = new[] { Row("a", "n", "Active") };
        var core = new[] { Row("a", "n", "Active") };
        var result = TableIntegrityComparer.Compare(dbo, core, cols);
        Assert.True(result.IsMatch);
        Assert.Equal(0, result.MissingInCoreCount);
        Assert.Equal(0, result.MissingInDboCount);
    }

    [Fact]
    public void Extra_dbo_rows_from_other_writers_are_not_a_mismatch()
    {
        var cols = new[] { "ExternalId", "Name", "Status" };
        var dbo = new[] { Row("a", "n", "Active"), Row("b", "ef-or-job", "Active") };
        var core = new[] { Row("a", "n", "Active") };
        var result = TableIntegrityComparer.Compare(dbo, core, cols);
        Assert.True(result.IsMatch);
        Assert.Equal(1, result.MissingInCoreCount);
        Assert.Equal(0, result.MissingInDboCount);
    }

    [Fact]
    public void Core_row_missing_from_dbo_is_mismatch()
    {
        var cols = new[] { "ExternalId", "Name", "Status" };
        var dbo = new[] { Row("a", "n", "Active") };
        var core = new[] { Row("a", "n", "Active"), Row("orphan", "sp-only", "Active") };
        var result = TableIntegrityComparer.Compare(dbo, core, cols);
        Assert.False(result.IsMatch);
        Assert.Equal(1, result.MissingInDboCount);
    }

    [Fact]
    public void Value_drift_on_sp_written_row_is_mismatch()
    {
        var cols = new[] { "ExternalId", "Name", "Status" };
        var dbo = new[] { Row("a", "n", "Active") };
        var core = new[] { Row("a", "n", "Closed") };
        var result = TableIntegrityComparer.Compare(dbo, core, cols);
        Assert.False(result.IsMatch);
        Assert.Equal(1, result.MissingInDboCount);
    }

    [Fact]
    public void Delete_on_both_sides_matches()
    {
        var cols = new[] { "ExternalId", "Name" };
        var result = TableIntegrityComparer.Compare([], [], cols);
        Assert.True(result.IsMatch);
    }

    private static IReadOnlyDictionary<string, string?> Row(string id, string name, string status = "Active") =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExternalId"] = id,
            ["Name"] = name,
            ["Status"] = status
        };
}

public class ParallelWriteExecutorTests
{
    [Fact]
    public async Task Core_failure_is_evidence_dbo_still_succeeds()
    {
        var store = new InMemoryParallelWriteStore();
        var executor = new ParallelWriteExecutor(store);
        var result = await executor.ExecuteAsync(
            "Upsert",
            "key-1",
            _ => Task.CompletedTask,
            _ => throw new InvalidOperationException("core boom"),
            2000);
        Assert.True(result.DboSucceeded);
        Assert.False(result.CoreSucceeded);
        Assert.Contains("core boom", result.CoreError);
        Assert.Equal(1, store.Snapshot().CoreFailures);
        Assert.Equal(0, store.Snapshot().DboFailures);
    }

    [Fact]
    public async Task Dbo_failure_is_thrown_after_both_settle()
    {
        var store = new InMemoryParallelWriteStore();
        var executor = new ParallelWriteExecutor(store);
        var coreRan = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            "Upsert",
            "key-1",
            _ => throw new InvalidOperationException("dbo boom"),
            _ => { coreRan = true; return Task.CompletedTask; },
            2000));
        Assert.True(coreRan);
        Assert.Equal(1, store.Snapshot().DboFailures);
    }
}
