using System.Text.Json;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record VideoEditReview(string Status, bool UpdateConfirmed, Video? Video = null, string? Reason = null);

/// <summary>Checks durable results and current state without ever resending a mutation.</summary>
public sealed class YouTubeVideoEditReconciler(YouTubeClient client)
{
    public async Task<VideoEditReview> InspectAsync(Guid actionId, UpdateVideoMetadataRequest expected,
        string confirmedChannel, CancellationToken ct = default)
    {
        YouTubeClient.ValidateMetadataEdit(expected);
        var action = await client.ReadMetadataEditAsync(actionId, ct);
        if (action.Status == "Blocked" && action.ConditionCode == "resource_changed")
            return new("Conflict", false, Reason: "The video changed before this edit could be applied. Review fresh details and obtain a new decision.");
        if (action.Status is not ("Completed" or "Indeterminate")) return new(action.Status, false);
        var channels = await client.ReadChannelAsync(ct);
        if (string.IsNullOrWhiteSpace(confirmedChannel) || channels.Items.Count != 1 || channels.NextPageToken is not null || channels.Items[0].Id != confirmedChannel)
            return new("ReviewRequired", false, Reason: "The saved channel connection could not be verified.");
        Video? saved = null;
        if (action.Status == "Completed")
        {
            try { saved = action.Result?.Deserialize<Video>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { }
            if (saved?.Snippet?.ChannelId != confirmedChannel || !YouTubeClient.MetadataMatches(saved, expected))
                return new("ReviewRequired", false, Reason: "The saved edit result does not confirm the approved metadata. Do not resend automatically.");
        }
        var page = await client.ReadVideoAsync(new(expected.VideoId), ct);
        var current = page.Items.Count == 1 && page.NextPageToken is null ? page.Items[0] : null;
        if (current?.Id != expected.VideoId || current.Snippet?.ChannelId != confirmedChannel)
            return new("ReviewRequired", saved is not null, Reason: "The current video could not be verified. No further change was sent.");
        var matches = YouTubeClient.MetadataMatches(current, expected);
        if (action.Status == "Indeterminate") return new("ReviewRequired", false, current,
            matches ? "The requested metadata is visible, but this does not prove which request applied it. Review the action; do not resend."
                : "The edit outcome remains uncertain. Review the current video; do not resend.");
        return matches ? new("Verified", true, current) : new("ChangedSinceCompletion", true, current,
            "The saved result confirms the edit, but the current metadata differs. Do not overwrite subsequent changes.");
    }
}
