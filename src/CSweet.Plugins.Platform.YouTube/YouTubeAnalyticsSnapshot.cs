using System.Globalization;
using System.Text.Json;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record OfficialMetricValue(string Name, string Label, string Unit, decimal? Value);
public sealed record YouTubeAnalyticsSnapshot(IReadOnlyList<OfficialMetricValue> Metrics)
{
    public bool HasData => Metrics.Any(x => x.Value is not null);

    // This fixed aggregate has no dimensions. Missing rows mean unavailable evidence, not zeros.
    public static YouTubeAnalyticsSnapshot From(OfficialAnalytics report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var expected = new (string Name, string Label, string Unit)[]
        {
            ("views", "Views", ""), ("estimatedMinutesWatched", "Watch time", "minutes"),
            ("averageViewDuration", "Average view duration", "seconds"),
            ("averageViewPercentage", "Average percentage viewed", "%"), ("likes", "Likes", ""),
            ("comments", "Comments", ""), ("shares", "Shares", ""),
            ("subscribersGained", "Subscribers gained", ""), ("subscribersLost", "Subscribers lost", "")
        };
        if (report.ColumnHeaders is null || report.ColumnHeaders.Count != expected.Length ||
            report.ColumnHeaders.Any(x => x is null || x.ColumnType != "METRIC" || x.DataType is not ("INTEGER" or "FLOAT")) ||
            report.ColumnHeaders.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != expected.Length ||
            expected.Any(x => !report.ColumnHeaders.Any(h => h.Name == x.Name)) || report.Rows?.Count > 1 || report.Rows?.Any(x => x is null) == true)
            throw new InvalidOperationException("The official analytics columns do not match this report.");
        var row = report.Rows?.SingleOrDefault();
        if (row is not null && row.Count != expected.Length) throw new InvalidOperationException("The official analytics row is incomplete.");
        var metrics = expected.Select(metric =>
        {
            var index = report.ColumnHeaders.Select((h, i) => (h, i)).Single(x => x.h.Name == metric.Name);
            decimal? value = null;
            if (row is not null)
            {
                var cell = row[index.i];
                decimal number = 0;
                var valid = cell.ValueKind == JsonValueKind.Number ? cell.TryGetDecimal(out number) :
                    cell.ValueKind == JsonValueKind.String && decimal.TryParse(cell.GetString(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out number);
                if (!valid || number < 0 || index.h.DataType == "INTEGER" && number != decimal.Truncate(number))
                    throw new InvalidOperationException("The official analytics value is invalid.");
                value = number;
            }
            return new OfficialMetricValue(metric.Name, metric.Label, metric.Unit, value);
        }).ToArray();
        return new(metrics);
    }
}
