using CodegraphChat.Infrastructure;
using Xunit;

namespace CodegraphChat.Tests;

public class ChatIntentRouterTests
{
    [Theory]
    [InlineData("who calls IndexingService", ChatIntent.Callers, "IndexingService")]
    [InlineData("callers of \"TopicChatService\"", ChatIntent.Callers, "TopicChatService")]
    [InlineData("what does CodegraphClient call", ChatIntent.Callees, "CodegraphClient")]
    [InlineData("impact of FileIntelligenceStore", ChatIntent.Impact, "FileIntelligenceStore")]
    [InlineData("index status", ChatIntent.Status, null)]
    [InlineData("show files", ChatIntent.Files, null)]
    [InlineData("tell me about EvidenceGraph", ChatIntent.Query, "EvidenceGraph")]
    public void Route_detects_intent_and_symbol(string message, ChatIntent expected, string? symbol)
    {
        var result = ChatIntentRouter.Route(message);
        Assert.Equal(expected, result.Intent);
        if (symbol is null)
            Assert.True(string.IsNullOrWhiteSpace(result.Symbol));
        else
            Assert.Equal(symbol, result.Symbol);
    }

    [Fact]
    public void Mode_override_wins()
    {
        var result = ChatIntentRouter.Route("anything IndexingService", "impact");
        Assert.Equal(ChatIntent.Impact, result.Intent);
        Assert.Equal("IndexingService", result.Symbol);
    }
}
