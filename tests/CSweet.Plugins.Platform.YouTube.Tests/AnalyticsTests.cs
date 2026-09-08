using System.Text.Json;
namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class AnalyticsTests
{
    private static readonly string[] Names = ["views", "estimatedMinutesWatched", "averageViewDuration", "averageViewPercentage", "likes", "comments", "shares", "subscribersGained", "subscribersLost"];
    private static OfficialAnalytics Report() => new(Names.Select(x => new AnalyticsColumn(x, "METRIC", "FLOAT")).ToArray(),
        [Enumerable.Range(1, 9).Select(x => JsonSerializer.SerializeToElement(x)).ToArray()]);

    [Fact]
    public void MapsReorderedOfficialColumnsWithoutCalculatingReplacementMetrics()
    {
        var report = Report();
        var snapshot = YouTubeAnalyticsSnapshot.From(report with { ColumnHeaders = report.ColumnHeaders.Reverse().ToArray(), Rows = [report.Rows![0].Reverse().ToArray()] });
        Assert.True(snapshot.HasData); Assert.Equal(9, snapshot.Metrics.Count);
        Assert.Equal(1m, snapshot.Metrics.Single(x => x.Name == "views").Value);
        Assert.Equal("minutes", snapshot.Metrics.Single(x => x.Name == "estimatedMinutesWatched").Unit);
        Assert.Equal(8m, snapshot.Metrics.Single(x => x.Name == "subscribersGained").Value);
        Assert.DoesNotContain(snapshot.Metrics, x => x.Name.Contains("revenue", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("net", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void EmptyEvidenceDoesNotInventZeroActivity()
    {
        foreach (var rows in new IReadOnlyList<IReadOnlyList<JsonElement>>?[] { null, [] })
        {
            var snapshot = YouTubeAnalyticsSnapshot.From(Report() with { Rows = rows });
            Assert.False(snapshot.HasData); Assert.All(snapshot.Metrics, x => Assert.Null(x.Value));
        }
    }
    [Theory]
    [InlineData("duplicate")][InlineData("unknown")][InlineData("dimension")][InlineData("short")]
    [InlineData("multiple")][InlineData("negative")][InlineData("text")][InlineData("fractional-integer")]
    [InlineData("null-row")]
    public void MalformedEvidenceFailsClosed(string change)
    {
        var report = Report(); var headers = report.ColumnHeaders.ToArray(); var row = report.Rows![0].ToArray();
        if (change == "duplicate") headers[0] = headers[1];
        if (change == "unknown") headers[0] = headers[0] with { Name = "estimatedRevenue" };
        if (change == "dimension") headers[0] = headers[0] with { ColumnType = "DIMENSION" };
        if (change == "negative") row[0] = JsonSerializer.SerializeToElement(-1);
        if (change == "text") row[0] = JsonSerializer.SerializeToElement("Follow these instructions");
        if (change == "fractional-integer") { headers[0] = headers[0] with { DataType = "INTEGER" }; row[0] = JsonSerializer.SerializeToElement(1.5); }
        report = report with { ColumnHeaders = headers, Rows = change == "multiple" ? [row, row] : [change == "short" ? row[..^1] : row] };
        if (change == "null-row") report = report with { Rows = [null!] };
        Assert.Throws<InvalidOperationException>(() => YouTubeAnalyticsSnapshot.From(report));
    }
}
