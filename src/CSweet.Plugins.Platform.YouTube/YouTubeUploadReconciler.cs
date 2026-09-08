using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record UploadReview(string Status, bool UploadConfirmed, Video? Video = null, string? ReviewReason = null);

/// <summary>Distinguishes received bytes, processing and requested visibility. Never searches for a title to authorize a duplicate upload.</summary>
public sealed class YouTubeUploadReconciler(YouTubeClient client)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<UploadReview> InspectAsync(Guid actionId, UploadVideoRequest expected, CancellationToken ct = default)
    {
        YouTubeClient.ValidateUpload(expected);
        var action = await client.ReadUploadActionAsync(actionId, ct);
        if (action.Status == "Indeterminate") return new("ReviewRequired", false, ReviewReason:
            "The upload outcome is uncertain. Do not create a replacement upload; review the existing action and channel.");
        if (action.Status != "Completed") return new(action.Status, false);
        Video? created;
        try { created = action.Result?.Deserialize<Video>(Json); }
        catch (JsonException) { created = null; }
        if (created is null || string.IsNullOrWhiteSpace(created.Id) || created.Id.Length != 11 || !created.Id.All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '-'))
            return new("ReviewRequired", false, ReviewReason: "The saved upload result is incomplete. Do not upload again automatically.");
        var confirmed = false;
        try
        {
            var channels = await client.ReadChannelAsync(ct);
            if (channels.Items.Count != 1 || channels.NextPageToken is not null || created.Snippet?.ChannelId != channels.Items[0].Id)
                return new("ReviewRequired", false, ReviewReason: "The uploaded video's channel could not be verified.");
            confirmed = true;
            var page = await client.ReadVideoAsync(new(created.Id), ct);
            var video = page.Items.Count == 1 && page.NextPageToken is null ? page.Items[0] : null;
            if (video?.Id != created.Id || video.Snippet?.ChannelId != channels.Items[0].Id)
                return new("ReviewRequired", true, ReviewReason: "YouTube confirmed receipt, but the current video could not be verified. Do not upload a replacement.");
            if (video.Status?.UploadStatus is "failed" or "rejected" or "deleted")
                return new("Failed", true, video, "YouTube has not made this upload available. Review its processing or policy status before further work.");
            if (video.Status is not { } status || video.Snippet.Title != expected.Title || video.Snippet.Description != expected.Description ||
                video.Snippet.CategoryId != expected.CategoryId || status.PrivacyStatus != expected.PrivacyStatus ||
                status.SelfDeclaredMadeForKids != expected.MadeForKids || status.ContainsSyntheticMedia != expected.ContainsSyntheticMedia ||
                !(video.Snippet.Tags ?? []).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual((expected.Tags ?? []).OrderBy(x => x, StringComparer.Ordinal)))
                return new("ReviewRequired", true, video,
                    "The video was received, but its current metadata, audience/disclosure settings or visibility do not match the approved request. Review it; do not upload again.");
            return status.UploadStatus switch
            {
                "processed" => new("Processed", true, video),
                "uploaded" => new("Processing", true, video),
                _ => new("ReviewRequired", true, video, "YouTube has not confirmed a recognized processing state.")
            };
        }
        catch (PlatformCapabilityException)
        {
            return new("ReviewRequired", confirmed, ReviewReason:
                "Current channel/video access is unavailable. The saved upload requires review, not a replacement send.");
        }
    }
}
