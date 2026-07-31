using DbIntelligence.Infrastructure;
using Xunit;

namespace DbIntelligence.Tests;

public class ProjectFolderDiscoveryTests
{
    [Fact]
    public void Discover_lists_child_folders_and_skips_noise()
    {
        var parent = Path.Combine(Path.GetTempPath(), "dbintel-parent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(parent, "AppOne"));
        Directory.CreateDirectory(Path.Combine(parent, "AppTwo"));
        Directory.CreateDirectory(Path.Combine(parent, "node_modules"));
        Directory.CreateDirectory(Path.Combine(parent, ".git"));
        File.WriteAllText(Path.Combine(parent, "AppOne", "AppOne.csproj"), "<Project />");

        try
        {
            var all = ProjectFolderDiscovery.Discover(parent, requireProjectMarkers: false);
            Assert.Equal(2, all.Count);
            Assert.Contains(all, x => x.Name == "AppOne" && x.HasMarker);
            Assert.Contains(all, x => x.Name == "AppTwo" && !x.HasMarker);

            var marked = ProjectFolderDiscovery.Discover(parent, requireProjectMarkers: true);
            Assert.Single(marked);
            Assert.Equal("AppOne", marked[0].Name);

            var rootOut = ProjectFolderDiscovery.ResolveArtifactsDirectory(all[0].Path, "");
            Assert.Equal(Path.GetFullPath(all[0].Path), rootOut);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}
