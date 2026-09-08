using System.Text.Json;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record PlaylistEditReview(string Status, bool UpdateConfirmed, Playlist? Playlist = null, string? Reason = null);

/// <summary>Checks saved receipts and current owned state. Never repeats an external mutation.</summary>
public sealed class YouTubePlaylistEditReconciler(YouTubeClient client)
{
    public async Task<PlaylistEditReview> InspectAsync(Guid actionId, UpdatePlaylistMetadataRequest expected,
        string confirmedChannel, CancellationToken ct = default)
    {
        YouTubeClient.ValidatePlaylistMetadataEdit(expected);
        var action = await client.ReadPlaylistMetadataEditAsync(actionId, ct);
        if (action.Status == "Blocked" && action.ConditionCode == "resource_changed")
            return new("Conflict", false, Reason: "The playlist changed. Review fresh details and obtain a new decision.");
        if (action.Status is not ("Completed" or "Indeterminate")) return new(action.Status, false);
        var channels = await client.ReadChannelAsync(ct);
        if (string.IsNullOrWhiteSpace(confirmedChannel) || channels.Items.Count != 1 || channels.NextPageToken is not null || channels.Items[0].Id != confirmedChannel)
            return new("ReviewRequired", false, Reason: "The saved channel connection could not be verified.");
        Playlist? saved = null;
        if (action.Status == "Completed")
        {
            try { saved = action.Result?.Deserialize<Playlist>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { }
            if (saved?.Snippet?.ChannelId != confirmedChannel || !YouTubeClient.PlaylistMetadataMatches(saved, expected))
                return new("ReviewRequired", false, Reason: "The saved result does not confirm the approved playlist edit. Do not resend.");
        }
        var page = await client.ReadPlaylistAsync(new(expected.PlaylistId), ct);
        var current = page.Items.Count == 1 && page.NextPageToken is null ? page.Items[0] : null;
        if (current?.Id != expected.PlaylistId || current.Snippet?.ChannelId != confirmedChannel)
            return new("ReviewRequired", saved is not null, Reason: "The current playlist could not be verified. No further change was sent.");
        if (action.Status == "Indeterminate")
            return new("ReviewRequired", false, current, "Current values cannot establish which request changed them. Review the uncertain action; do not resend.");
        return YouTubeClient.PlaylistMetadataMatches(current, expected) ? new("Verified", true, current) :
            new("ChangedSinceCompletion", true, current, "The saved result confirms the edit, but the playlist has changed since. Do not overwrite later changes.");
    }
}
