using System.Text.Json.Serialization;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record ListMembersRequest([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PageToken = null);
public sealed record ListMembershipLevelsRequest;
public sealed record ChannelMember(ChannelMemberSnippet Snippet);
public sealed record ChannelMemberSnippet(string CreatorChannelId, MemberProfile? MemberDetails, MembershipDetails MembershipsDetails);
public sealed record MemberProfile(string? ChannelId = null, string? DisplayName = null);
public sealed record MembershipDetails(string HighestAccessibleLevel, string HighestAccessibleLevelDisplayName,
    IReadOnlyList<string> AccessibleLevels, MembershipDuration MembershipsDuration, IReadOnlyList<MembershipLevelDuration> MembershipsDurationAtLevel);
public sealed record MembershipDuration(DateTimeOffset MemberSince, int MemberTotalDurationMonths);
public sealed record MembershipLevelDuration(string Level, DateTimeOffset MemberSince, int MemberTotalDurationMonths);
public sealed record MembershipLevel(string Id, MembershipLevelSnippet Snippet);
public sealed record MembershipLevelSnippet(string CreatorChannelId, MembershipLevelDetails LevelDetails);
public sealed record MembershipLevelDetails(string DisplayName);

public sealed partial class YouTubeClient
{
    public async Task<YouTubePage<ChannelMember>> ListMembersAsync(ListMembersRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(request);
        if (request.PageToken is { } token && !MembershipText(token, 2048)) throw new ArgumentException("Invalid membership page token.");
        var page = await platform.InvokeAsync<ListMembersRequest, YouTubePage<ChannelMember>>(YouTubeCapabilities.ListMembers, request, ct);
        if (page.Items is null || page.Items.Count > 25 || page.NextPageToken is { } next && !MembershipText(next, 2048)) InvalidMembership();
        foreach (var item in page.Items!)
        {
            var s = item?.Snippet;
            if (s is null || !MembershipText(s.CreatorChannelId, 128) ||
                s.MemberDetails is { } profile && (profile.ChannelId is { } id && !MembershipText(id, 128) || profile.DisplayName is { } name && !MembershipText(name, 200))) InvalidMembership();
            var details = s!.MembershipsDetails;
            if (details is null || !MembershipText(details.HighestAccessibleLevel, 128) || !MembershipText(details.HighestAccessibleLevelDisplayName, 200) ||
                details.AccessibleLevels is null || details.AccessibleLevels.Count > 100 || details.AccessibleLevels.Any(x => !MembershipText(x, 128)) ||
                details.MembershipsDuration is null || details.MembershipsDuration.MemberSince == default || details.MembershipsDuration.MemberTotalDurationMonths < 0 ||
                details.MembershipsDurationAtLevel is null || details.MembershipsDurationAtLevel.Count > 100 ||
                details.MembershipsDurationAtLevel.Any(x => x is null || !MembershipText(x.Level, 128) || x.MemberSince == default || x.MemberTotalDurationMonths < 0)) InvalidMembership();
        }
        return page;
    }

    public async Task<YouTubePage<MembershipLevel>> ListMembershipLevelsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var page = await platform.InvokeAsync<ListMembershipLevelsRequest, YouTubePage<MembershipLevel>>(YouTubeCapabilities.ListMembershipLevels, new(), ct);
        if (page.Items is null || page.Items.Count > 100 || page.NextPageToken is not null) InvalidMembership();
        foreach (var level in page.Items!)
            if (level is null || !MembershipText(level.Id, 128) || level.Snippet is null || !MembershipText(level.Snippet.CreatorChannelId, 128) ||
                level.Snippet.LevelDetails is null || !MembershipText(level.Snippet.LevelDetails.DisplayName, 200)) InvalidMembership();
        if (page.Items!.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != page.Items.Count) InvalidMembership();
        return page;
    }
    private static bool MembershipText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Any(char.IsControl);
    private static void InvalidMembership() => throw new InvalidOperationException("Membership data could not be verified safely.");
}
