using CSweet.Agent.SDK;
using System.Text.Json;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record ReplyMatch(string Id, string? PublishedAt);
public sealed record ReplyReconciliation(string Status, YouTubeComment? ConfirmedReply,
    IReadOnlyList<ReplyMatch> PossibleMatches, bool ScanComplete, string? ReviewReason);

/// <summary>Read-only recovery. Search matches never prove that this particular send succeeded.</summary>
public sealed class YouTubeReplyReconciler(YouTubeClient youtube)
{
    public async Task<ReplyReconciliation> InspectAsync(Guid actionId, ReplyToCommentRequest expected, CancellationToken ct = default)
    {
        YouTubeClient.ValidateReply(expected);
        var action = await youtube.ReadReplyActionAsync(actionId, ct);
        if (action.Status == "Completed")
        {
            YouTubeComment? reply;
            try { reply = action.Result?.Deserialize<YouTubeComment>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { return new("ReviewRequired", null, [], false, "The saved provider result is malformed. Do not repost."); }
            if (reply is not null && !string.IsNullOrWhiteSpace(reply.Id) && reply.Snippet?.ParentId == expected.ParentId && reply.Snippet.TextOriginal == expected.Text)
                return new("Completed", reply, [], true, null);
            return new("ReviewRequired", null, [], false, "The saved provider result does not match the requested reply. Do not repost.");
        }
        if (action.Status != "Indeterminate") return new(action.Status, null, [], false, null);
        var matches = new Dictionary<string, ReplyMatch>(StringComparer.Ordinal);
        try
        {
            var channel = await youtube.ReadChannelAsync(ct);
            if (channel.Items.Count != 1 || !string.IsNullOrEmpty(channel.NextPageToken))
                return Review(false, "The connected channel could not be verified for recovery.");
            var author = channel.Items[0].Id;
            var tokens = new HashSet<string>(StringComparer.Ordinal); string? token = null;
            for (var pageNumber = 0; pageNumber < 10; pageNumber++)
            {
                var page = await youtube.ListCommentRepliesAsync(new(expected.ParentId, token), ct);
                foreach (var reply in page.Items)
                    if (reply.Snippet?.ParentId == expected.ParentId && reply.Snippet.TextOriginal == expected.Text &&
                        reply.Snippet.AuthorChannelId?.Value == author && matches.Count < 10)
                        matches.TryAdd(reply.Id, new(reply.Id, reply.Snippet.PublishedAt));
                token = page.NextPageToken;
                if (string.IsNullOrEmpty(token)) return Review(true, "Review the channel before any further reply. Matching text does not prove which request posted it, and missing text does not prove failure.");
                if (token.Length > 1024 || !tokens.Add(token)) break;
            }
            return Review(false, "The recovery search is incomplete. Do not repost while the original result remains uncertain.");
        }
        catch (PlatformCapabilityException)
        {
            return Review(false, "Recovery reads are unavailable. Restore access and review the channel before doing more work.");
        }

        ReplyReconciliation Review(bool complete, string reason) => new("ReviewRequired", null, matches.Values.ToArray(), complete, reason);
    }
}
