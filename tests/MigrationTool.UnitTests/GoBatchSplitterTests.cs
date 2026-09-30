using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class GoBatchSplitterTests
{
    [Fact]
    public void Split_RemovesGoLinesAndKeepsBatchBoundaries()
    {
        var result = GoBatchSplitter.Split("SELECT 1;\r\nGO\r\nSELECT 2;");

        Assert.True(result.RemovedGo);
        Assert.Equal(2, result.Batches.Count);
        Assert.Equal("SELECT 1;", result.Batches[0]);
        Assert.Equal("SELECT 2;", result.Batches[1]);
        Assert.DoesNotContain("GO", result.NormalizedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(GoBatchSplitter.BatchMarker, result.NormalizedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_RepeatsBatchWhenGoHasACount()
    {
        var result = GoBatchSplitter.Split("SELECT 1;\nGO 2\n");

        Assert.Equal(2, result.Batches.Count);
        Assert.All(result.Batches, batch => Assert.Equal("SELECT 1;", batch));
    }

    [Fact]
    public void Split_KeepsGoInsideAStatement()
    {
        var result = GoBatchSplitter.Split("SELECT 'GO' AS token;");

        Assert.False(result.RemovedGo);
        Assert.Single(result.Batches);
        Assert.Contains("'GO'", result.Batches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Split_ReadsSavedBatchMarkers()
    {
        var saved = GoBatchSplitter.Split("SELECT 1;\nGO\nSELECT 2;").NormalizedText;
        var again = GoBatchSplitter.Split(saved);

        Assert.False(again.RemovedGo);
        Assert.Equal(2, again.Batches.Count);
    }
}
