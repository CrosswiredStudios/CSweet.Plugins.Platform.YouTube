using System.Text.RegularExpressions;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record ListVideoCategoriesRequest(string RegionCode, string Language = "en_US");
public sealed record VideoCategory(string Id, VideoCategorySnippet Snippet);
public sealed record VideoCategorySnippet(string Title, bool Assignable);

public sealed partial class YouTubeClient
{
    public async Task<YouTubePage<VideoCategory>> ListVideoCategoriesAsync(ListVideoCategoriesRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.RegionCode is null || request.RegionCode.Length != 2 || request.RegionCode.Any(x => x is < 'A' or > 'Z') ||
            request.Language is null || request.Language.Length > 21 || !Regex.IsMatch(request.Language, @"\A[A-Za-z]{2,3}([-_][A-Za-z0-9]{2,8}){0,2}\z"))
            throw new ArgumentException("Choose a country and supported display language.", nameof(request));
        var result = await platform.InvokeAsync<ListVideoCategoriesRequest, YouTubePage<VideoCategory>>(
            YouTubeCapabilities.ListVideoCategories, request, ct);
        if (result.Items is null || result.Items.Count > 100 || result.Items.Any(x => x is null ||
            string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 3 || !x.Id.All(char.IsAsciiDigit) || x.Snippet is null ||
            string.IsNullOrWhiteSpace(x.Snippet.Title) || x.Snippet.Title.Length > 200 || x.Snippet.Title.Any(char.IsControl)) ||
            result.Items.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != result.Items.Count)
            throw new InvalidOperationException("YouTube returned an incomplete or ambiguous category catalog.");
        return result;
    }

    /// <summary>Select by the actual display name. No guessed ID, default category or silent partial catalog.</summary>
    public async Task<VideoCategory> ResolveVideoCategoryAsync(ListVideoCategoriesRequest request, string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
            throw new ArgumentException("Choose a category by its displayed name.", nameof(displayName));
        var page = await ListVideoCategoriesAsync(request, ct);
        if (page.NextPageToken is not null)
            throw new InvalidOperationException("The category catalog is incomplete. Review available categories before preparing an upload.");
        var matches = page.Items.Where(x => x.Snippet.Assignable &&
            string.Equals(x.Snippet.Title, displayName.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Choose one currently assignable category from YouTube's list.");
        return matches[0];
    }
}
