using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public sealed class SqlProjectPathResolverTests
{
    [Fact]
    public void Resolves_sqlproj_file()
    {
        var dir = CreateTempDir();
        var project = Path.Combine(dir, "Sample.Database.sqlproj");
        File.WriteAllText(project, """<Project Sdk="Microsoft.Build.Sql"></Project>""");

        Assert.Equal(project, SqlProjectPathResolver.Resolve(project));
    }

    [Fact]
    public void Resolves_folder_with_single_sqlproj()
    {
        var dir = CreateTempDir();
        var project = Path.Combine(dir, "Only.sqlproj");
        File.WriteAllText(project, """<Project Sdk="Microsoft.Build.Sql"></Project>""");

        Assert.Equal(project, SqlProjectPathResolver.Resolve(dir));
    }

    [Fact]
    public void Rejects_folder_with_multiple_sqlproj()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "A.sqlproj"), "<Project />");
        File.WriteAllText(Path.Combine(dir, "B.sqlproj"), "<Project />");

        Assert.Throws<InvalidOperationException>(() => SqlProjectPathResolver.Resolve(dir));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "migration-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
