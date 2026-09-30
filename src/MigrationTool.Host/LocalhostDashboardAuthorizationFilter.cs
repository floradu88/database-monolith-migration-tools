using System.Net;
using Hangfire;
using Hangfire.Dashboard;

namespace MigrationTool.Host;

public sealed class LocalhostDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var remote = context.GetHttpContext().Connection.RemoteIpAddress;
        if (remote is null)
        {
            return false;
        }

        if (remote.IsIPv4MappedToIPv6)
        {
            remote = remote.MapToIPv4();
        }

        return IPAddress.IsLoopback(remote);
    }
}
