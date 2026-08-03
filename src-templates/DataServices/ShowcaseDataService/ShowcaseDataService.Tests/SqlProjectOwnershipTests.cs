using Xunit;

namespace ShowcaseDataService.Tests;

/// <summary>
/// Guards the golden hybrid model: SQL Build vs EF vs Cutover (None).
/// </summary>
public class SqlProjectOwnershipTests
{
    private static string DatabaseRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "ShowcaseDataService.Database"));

    [Fact]
    public void ObjectOwnership_Declares_NonOverlapping_Owners()
    {
        var path = Path.Combine(DatabaseRoot, "object-ownership.yml");
        Assert.True(File.Exists(path), $"Missing {path}");
        var yml = File.ReadAllText(path);
        Assert.Contains("showcase.GetShowcaseSummary", yml);
        Assert.Contains("showcase.Items", yml);
        Assert.Contains("ShowcaseDataService.Database", yml);
        Assert.Contains("ShowcaseDataService.Migrations", yml);
        Assert.Contains("cutover_scripts:", yml);
    }

    [Fact]
    public void SqlProj_Builds_Sp_Not_EfTable()
    {
        var sqlproj = File.ReadAllText(Path.Combine(DatabaseRoot, "ShowcaseDataService.Database.sqlproj"));
        Assert.Contains(@"Build Include=""Programmability\GetShowcaseSummary.sql""", sqlproj);
        Assert.Contains(@"Build Include=""Contract\DatabaseContract.sql""", sqlproj);
        Assert.DoesNotContain(@"Tables\Items.sql", sqlproj);
        Assert.Contains(@"None Include=""Reference\EfOwned\Items.reference.sql""", sqlproj);
        Assert.Contains(@"None Include=""Cutover\**\*.sql""", sqlproj);
    }

    [Fact]
    public void Cutover_Scripts_Have_Matching_Up_Down_Pairs()
    {
        var cutover = Path.Combine(DatabaseRoot, "Cutover");
        Assert.True(Directory.Exists(cutover));
        var ups = Directory.GetFiles(cutover, "*.up.sql").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.NotEmpty(ups);
        foreach (var up in ups)
        {
            var down = up.Replace(".up.sql", ".down.sql", StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(down), $"Missing down script for {Path.GetFileName(up)}");
        }
    }

    [Fact]
    public void EfOwned_Items_Are_Reference_Only()
    {
        Assert.False(File.Exists(Path.Combine(DatabaseRoot, "Tables", "Items.sql")));
        var reference = Path.Combine(DatabaseRoot, "Reference", "EfOwned", "Items.reference.sql");
        Assert.True(File.Exists(reference));
        var text = File.ReadAllText(reference);
        Assert.Contains("REFERENCE ONLY", text);
        Assert.Contains("Ownership: EF", text);
        Assert.DoesNotContain("CREATE TABLE [showcase].[Items]", text.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));
    }
}
