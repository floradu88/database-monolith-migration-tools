using System.Reflection;
using ShowcaseDataService.Application;
using Xunit;

namespace ShowcaseDataService.Tests;

/// <summary>
/// Architecture smoke: Application must not reference SQL client / Dapper directly.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Application_DoesNotReference_SqlClient_Or_Dapper()
    {
        var asm = typeof(ShowcaseItemService).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", refs);
        Assert.DoesNotContain("Dapper", refs);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", refs);
    }
}
