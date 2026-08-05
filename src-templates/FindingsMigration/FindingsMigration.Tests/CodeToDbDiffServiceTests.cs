using FindingsMigration.Contracts;
using FindingsMigration.Core;
using Xunit;

namespace FindingsMigration.Tests;

public class CodeToDbDiffServiceTests
{
    [Fact]
    public void Diff_returns_only_new_extracted_edges()
    {
        var previous = new CodeToDbMapDocument
        {
            Entries =
            [
                Entry("A.Get", "dbo.Customer", "EXTRACTED"),
                Entry("B.Dyn", "dbo.X", "AMBIGUOUS")
            ]
        };
        var current = new CodeToDbMapDocument
        {
            Entries =
            [
                Entry("A.Get", "dbo.Customer", "EXTRACTED"),
                Entry("C.New", "dbo.Order", "EXTRACTED"),
                Entry("D.Also", "dbo.Y", "AMBIGUOUS"),
                Entry("B.Dyn", "dbo.X", "EXTRACTED") // same key as previous AMBIGUOUS — new as EXTRACTED
            ]
        };

        var result = new CodeToDbDiffService().Diff(previous, current);

        Assert.Equal(1, result.PreviousExtractedCount);
        Assert.Equal(3, result.CurrentExtractedCount);
        Assert.Equal(2, result.NewExtractedCount);
        Assert.Contains(result.NewExtractedEntries, e => e.CodeLabel == "C.New");
        Assert.Contains(result.NewExtractedEntries, e => e.CodeLabel == "B.Dyn");
        Assert.DoesNotContain(result.NewExtractedEntries, e => e.CodeLabel == "A.Get");
    }

    [Fact]
    public void DiffFiles_and_WriteDiffDocument_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "diff-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var prevPath = Path.Combine(root, "prev.json");
            var currPath = Path.Combine(root, "curr.json");
            var outPath = Path.Combine(root, "new.json");
            File.WriteAllText(prevPath, """
                {"entries":[{"codeLabel":"Old","dbObject":"dbo.A","relation":"READS","confidence":"EXTRACTED","pattern":"ef-linq"}]}
                """);
            File.WriteAllText(currPath, """
                {"entries":[
                  {"codeLabel":"Old","dbObject":"dbo.A","relation":"READS","confidence":"EXTRACTED","pattern":"ef-linq"},
                  {"codeLabel":"New","dbObject":"dbo.B","relation":"READS","confidence":"EXTRACTED","pattern":"ef-linq"}
                ]}
                """);

            var svc = new CodeToDbDiffService();
            var result = svc.DiffFiles(prevPath, currPath);
            Assert.Equal(1, result.NewExtractedCount);
            svc.WriteDiffDocument(result, outPath);
            Assert.True(File.Exists(outPath));
            Assert.Contains("New", File.ReadAllText(outPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CodeToDbEntry Entry(string code, string db, string confidence) => new()
    {
        CodeLabel = code,
        CodeNodeId = "code:" + code,
        DbObject = db,
        DbNodeId = "db:" + db,
        Relation = "READS",
        Confidence = confidence,
        Pattern = "ef-linq",
        DbKind = "Table"
    };
}
