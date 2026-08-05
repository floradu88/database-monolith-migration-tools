using CodegraphChat.Contracts;
using CodegraphChat.Infrastructure;
using CodegraphChat.Infrastructure.Codegraph;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodegraphChat.Tests;

internal sealed class FakeCodegraphClient : ICodegraphClient
{
    public bool Available { get; set; } = true;
    public string VersionDetail { get; set; } = "1.0.0";
    public CliResult StatusResult { get; set; } = new(true, 0, """{"indexed":true}""", "");
    public CliResult EnsureResult { get; set; } = new(true, 0, "synced", "");
    public CliResult QueryResult { get; set; } = new(
        true,
        0,
        """[{"name":"IndexingService","kind":"class","file":"IndexingService.cs"}]""",
        "");
    public CliResult CallersResult { get; set; } = new(true, 0, "[]", "");
    public CliResult CalleesResult { get; set; } = new(true, 0, "[]", "");
    public CliResult ImpactResult { get; set; } = new(true, 0, """{"nodes":[]}""", "");
    public CliResult FilesResult { get; set; } = new(true, 0, "src/", "");
    public int EnsureCalls { get; private set; }
    public string? LastEnsurePath { get; private set; }

    public Task<(bool Available, string Detail)> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((Available, VersionDetail));

    public Task<CliResult> StatusAsync(string repositoryPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(StatusResult);

    public Task<CliResult> EnsureIndexAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        EnsureCalls++;
        LastEnsurePath = repositoryPath;
        return Task.FromResult(EnsureResult);
    }

    public Task<CliResult> QueryAsync(string repositoryPath, string search, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(QueryResult);

    public Task<CliResult> CallersAsync(string repositoryPath, string symbol, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(CallersResult);

    public Task<CliResult> CalleesAsync(string repositoryPath, string symbol, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(CalleesResult);

    public Task<CliResult> ImpactAsync(string repositoryPath, string symbol, int depth, CancellationToken cancellationToken = default) =>
        Task.FromResult(ImpactResult);

    public Task<CliResult> FilesAsync(string repositoryPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(FilesResult);
}

public class TopicChatServiceTests
{
    private static TopicChatService CreateService(
        FakeCodegraphClient fake,
        string? configuredRepo = null,
        IChatSessionStore? session = null)
    {
        var options = Options.Create(new CodegraphChatOptions
        {
            TargetRepositoryPath = configuredRepo,
            DefaultQueryLimit = 10,
            DefaultImpactDepth = 2,
            ProcessTimeoutSeconds = 30
        });
        return new TopicChatService(fake, options, session ?? new ChatSessionStore());
    }

    [Fact]
    public async Task EnsureIndex_requires_bound_path()
    {
        var fake = new FakeCodegraphClient();
        var svc = CreateService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnsureIndexAsync());
        Assert.Equal(0, fake.EnsureCalls);
    }

    [Fact]
    public async Task EnsureIndex_succeeds_when_session_bound()
    {
        var temp = Directory.CreateTempSubdirectory("codegraphchat-test-");
        try
        {
            var fake = new FakeCodegraphClient();
            var session = new ChatSessionStore { RepositoryPath = temp.FullName };
            var svc = CreateService(fake, session: session);

            var result = await svc.EnsureIndexAsync();

            Assert.True(result.EnsureSucceeded);
            Assert.Equal(1, fake.EnsureCalls);
            Assert.Equal(temp.FullName, fake.LastEnsurePath);
            Assert.Equal(temp.FullName, result.RepositoryPath);
            Assert.Contains("synced", result.EnsureDetail ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { temp.Delete(recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task GetSession_indexReady_when_codegraph_folder_exists()
    {
        var temp = Directory.CreateTempSubdirectory("codegraphchat-ready-");
        try
        {
            Directory.CreateDirectory(Path.Combine(temp.FullName, ".codegraph"));
            var fake = new FakeCodegraphClient
            {
                StatusResult = new CliResult(false, 1, "", "status schema unknown")
            };
            var session = new ChatSessionStore { RepositoryPath = temp.FullName };
            var svc = CreateService(fake, session: session);

            var result = await svc.GetSessionAsync();

            Assert.True(result.IndexReady);
            Assert.Equal(temp.FullName, result.RepositoryPath);
        }
        finally
        {
            try { temp.Delete(recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Ask_query_mode_includes_evidence_and_symbol_list()
    {
        var temp = Directory.CreateTempSubdirectory("codegraphchat-ask-");
        try
        {
            var fake = new FakeCodegraphClient();
            var session = new ChatSessionStore { RepositoryPath = temp.FullName };
            var svc = CreateService(fake, session: session);

            var response = await svc.AskAsync(new ChatRequest
            {
                Message = "tell me about IndexingService",
                Mode = "query"
            });

            Assert.False(string.IsNullOrWhiteSpace(response.ConversationId));
            Assert.Equal("query", response.DetectedMode);
            Assert.Contains("IndexingService", response.Reply.Content, StringComparison.Ordinal);
            Assert.NotEmpty(response.Evidence);
            Assert.Equal("query", response.Evidence[0].Command);
            Assert.True(response.Evidence[0].Succeeded);
        }
        finally
        {
            try { temp.Delete(recursive: true); } catch { /* ignore */ }
        }
    }
}
