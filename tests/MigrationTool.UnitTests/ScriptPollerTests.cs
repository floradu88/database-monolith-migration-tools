using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MigrationTool.Application;
using MigrationTool.Domain;
using MigrationTool.Infrastructure;

namespace MigrationTool.UnitTests;

public class ScriptPollerTests
{
    [Fact]
    public async Task Poll_MovesSuccessfulChangeScriptAndStripsGo()
    {
        using var fixture = new PollFixture();
        fixture.Add("ddl/create_city.sql", "SELECT 1;\nGO\nSELECT 2;");

        var summary = await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.Equal(1, summary.Succeeded);
        Assert.False(File.Exists(Path.Combine(fixture.Inbox, "ddl", "create_city.sql")));
        var saved = File.ReadAllText(Path.Combine(fixture.Success, "create_city.sql"));
        Assert.DoesNotContain("GO", saved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(GoBatchSplitter.BatchMarker, saved, StringComparison.Ordinal);
        var executed = Assert.Single(fixture.Changes.Calls);
        Assert.Equal(2, executed.Batches.Count);
        Assert.Equal(42, executed.CommandTimeoutSeconds);
    }

    [Fact]
    public async Task Poll_WritesNumberedFailureAndKeepsInboxCopy()
    {
        using var fixture = new PollFixture();
        fixture.Changes.Handler = _ => ScriptRunResult.Fail("database says no");
        fixture.Add("dml/update_city.sql", "UPDATE city SET name = 'A';");

        var summary = await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.True(File.Exists(Path.Combine(fixture.Inbox, "dml", "update_city.sql")));
        var error = File.ReadAllText(Path.Combine(fixture.Failed, "001_update_city.error.txt"));
        Assert.Contains("attempt: 1", error, StringComparison.Ordinal);
        Assert.Contains("database says no", error, StringComparison.Ordinal);
        Assert.Contains("UPDATE city SET name = 'A';", File.ReadAllText(Path.Combine(fixture.Failed, "001_update_city.sql")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Poll_RemovesScriptAfterFourthFailure()
    {
        using var fixture = new PollFixture();
        fixture.Changes.Handler = _ => ScriptRunResult.Fail("still broken");
        fixture.Add("dml/update_city.sql", "UPDATE city SET name = 'A';");

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await fixture.Poller.PollAsync(CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(fixture.Failed, $"{attempt:000}_update_city.error.txt")));
        }

        Assert.False(File.Exists(Path.Combine(fixture.Inbox, "dml", "update_city.sql")));
        Assert.False(File.Exists(Path.Combine(fixture.Failed, "005_update_city.sql")));
    }

    [Fact]
    public async Task Poll_DropsInboxScriptWhenFourFailuresAlreadyExist()
    {
        using var fixture = new PollFixture();
        fixture.Add("dml/update_city.sql", "UPDATE city SET name = 'A';");
        File.WriteAllText(Path.Combine(fixture.Failed, "004_update_city.sql"), "old");

        await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(fixture.Inbox, "dml", "update_city.sql")));
        Assert.Empty(fixture.Changes.Calls);
        Assert.False(File.Exists(Path.Combine(fixture.Failed, "005_update_city.sql")));
    }

    [Fact]
    public async Task Poll_LeavesQueryScriptInInboxAndWritesResult()
    {
        using var fixture = new PollFixture();
        fixture.Add("query/count_city.sql", "SELECT COUNT(*) FROM city;");

        await fixture.Poller.PollAsync(CancellationToken.None);
        await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(fixture.Inbox, "query", "count_city.sql")));
        Assert.Equal(2, fixture.Queries.Calls.Count);
        Assert.Empty(fixture.Changes.Calls);
        Assert.Equal(2, Directory.GetFiles(fixture.Success, "*.result.txt").Length);
    }

    [Fact]
    public async Task Poll_RunsScriptsInKindOrder()
    {
        using var fixture = new PollFixture();
        fixture.Add("query/q.sql", "SELECT 1;");
        fixture.Add("data/d.sql", "SELECT 1;");
        fixture.Add("dml/m.sql", "SELECT 1;");
        fixture.Add("ddl/c.sql", "SELECT 1;");

        await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.Equal(
            ["ddl/c.sql", "dml/m.sql", "data/d.sql"],
            fixture.Changes.Calls.Select(call => call.JournalName).ToArray());
        Assert.Equal("query/q.sql", Assert.Single(fixture.Queries.Calls).Name);
    }

    [Theory]
    [InlineData(DatabaseProviderKind.SqlServer, "ADD [Title] nvarchar(max) NULL", "INFORMATION_SCHEMA.COLUMNS")]
    [InlineData(DatabaseProviderKind.PostgreSql, "ADD COLUMN \"title\" text NULL", "information_schema.columns")]
    public async Task Poll_RepairsMissingColumnThenRetries(DatabaseProviderKind provider, string alterFragment, string diagnosticFragment)
    {
        using var fixture = new PollFixture(provider);
        var originalCalls = 0;
        fixture.Changes.Handler = script =>
        {
            if (script.JournalName.StartsWith("pre_add_", StringComparison.Ordinal))
            {
                return ScriptRunResult.Ok();
            }

            originalCalls++;
            return originalCalls == 1
                ? MissingColumn(provider)
                : ScriptRunResult.Ok();
        };
        fixture.Queries.Handler = request =>
            request.Name.StartsWith("diagnostic:", StringComparison.Ordinal)
                ? ScriptRunResult.Ok("column_name | title\n(1 row)")
                : ScriptRunResult.Ok("ok");

        var script = provider == DatabaseProviderKind.PostgreSql
            ? "INSERT INTO public.books (title) VALUES ('Dune');"
            : "INSERT INTO dbo.Books (Title) VALUES ('Dune');";
        fixture.Add("dml/add_title.sql", script);

        var summary = await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(1, summary.Repaired);
        Assert.Contains(fixture.Changes.Calls, call => call.JournalName.StartsWith("pre_add_", StringComparison.Ordinal) && call.Batches[0].Contains(alterFragment, StringComparison.Ordinal));
        Assert.Contains(fixture.Queries.Calls, call => call.Sql.Contains(diagnosticFragment, StringComparison.Ordinal));
        Assert.Equal(42, fixture.Queries.Calls[0].CommandTimeoutSeconds);
        Assert.All(fixture.Changes.Calls, call => Assert.Equal(42, call.CommandTimeoutSeconds));
        Assert.True(Directory.GetFiles(fixture.Success, "pre_add_*.sql").Length == 1);
        Assert.True(Directory.GetFiles(fixture.Success, "*.diagnostics.txt").Length == 1);
        Assert.False(File.Exists(Path.Combine(fixture.Inbox, "dml", "add_title.sql")));
    }

    [Fact]
    public async Task Poll_SkipsWhenConnectionStringIsEmpty()
    {
        using var fixture = new PollFixture { ConnectionString = "" };
        fixture.Add("ddl/create_city.sql", "SELECT 1;");

        var summary = await fixture.Poller.PollAsync(CancellationToken.None);

        Assert.True(summary.ConnectionSkipped);
        Assert.True(File.Exists(Path.Combine(fixture.Inbox, "ddl", "create_city.sql")));
        Assert.Empty(fixture.Changes.Calls);
    }

    private static ScriptRunResult MissingColumn(DatabaseProviderKind provider) =>
        provider == DatabaseProviderKind.PostgreSql
            ? ScriptRunResult.Fail("42703: column \"title\" of relation \"books\" does not exist", sqlState: "42703")
            : ScriptRunResult.Fail("Invalid column name 'Title'.", number: 207);

    private sealed class PollFixture : IDisposable
    {
        public PollFixture(DatabaseProviderKind provider = DatabaseProviderKind.SqlServer)
        {
            Root = Path.Combine(Path.GetTempPath(), "migration-tool-tests", Guid.NewGuid().ToString("N"));
            Inbox = Path.Combine(Root, "inbox");
            Success = Path.Combine(Root, "success");
            Failed = Path.Combine(Root, "failed");
            Options = new MigrationOptions
            {
                Provider = provider,
                ConnectionString = "Server=unused;",
                InboxPath = Inbox,
                SuccessPath = Success,
                FailedPath = Failed,
                MaxRetries = 4,
                CommandTimeoutSeconds = 42,
                RepairMissingColumns = true
            };
            Store = new FileScriptStore(Microsoft.Extensions.Options.Options.Create(Options), new TestEnvironment(Root));
            Poller = new ScriptPoller(Microsoft.Extensions.Options.Options.Create(Options), Store, Changes, Queries, NullLogger<ScriptPoller>.Instance);
        }

        public string Root { get; }

        public string Inbox { get; }

        public string Success { get; }

        public string Failed { get; }

        public MigrationOptions Options { get; }

        public FileScriptStore Store { get; }

        public FakeChangeRunner Changes { get; } = new();

        public FakeQueryRunner Queries { get; } = new();

        public ScriptPoller Poller { get; }

        public string ConnectionString
        {
            set => Options.ConnectionString = value;
        }

        public void Add(string relativePath, string contents)
        {
            var path = Path.Combine(Inbox, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FakeChangeRunner : IChangeScriptRunner
    {
        public List<ChangeScript> Calls { get; } = [];

        public Func<ChangeScript, ScriptRunResult>? Handler { get; set; }

        public Task<ScriptRunResult> ExecuteAsync(ChangeScript script, CancellationToken cancellationToken)
        {
            Calls.Add(script);
            return Task.FromResult(Handler?.Invoke(script) ?? ScriptRunResult.Ok());
        }
    }

    private sealed class FakeQueryRunner : IQueryScriptRunner
    {
        public List<QueryScriptRequest> Calls { get; } = [];

        public Func<QueryScriptRequest, ScriptRunResult>? Handler { get; set; }

        public Task<ScriptRunResult> ExecuteAsync(QueryScriptRequest script, CancellationToken cancellationToken)
        {
            Calls.Add(script);
            return Task.FromResult(Handler?.Invoke(script) ?? ScriptRunResult.Ok("probe | 1\n(1 row)"));
        }
    }

    private sealed class TestEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "MigrationTool.UnitTests";

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
