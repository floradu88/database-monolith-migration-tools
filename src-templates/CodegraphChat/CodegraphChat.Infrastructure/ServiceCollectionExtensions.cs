using CodegraphChat.Infrastructure.Codegraph;
using Microsoft.Extensions.DependencyInjection;

namespace CodegraphChat.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCodegraphChat(this IServiceCollection services)
    {
        services.AddSingleton<CliProcessRunner>();
        services.AddSingleton<IChatSessionStore, ChatSessionStore>();
        services.AddSingleton<ICodegraphClient, CodegraphClient>();
        services.AddSingleton<ITopicChatService, TopicChatService>();
        return services;
    }
}
