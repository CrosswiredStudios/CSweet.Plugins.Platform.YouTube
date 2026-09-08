using CSweet.Agent.SDK;
using System.Text.Json;

namespace CSweet.Plugins.Platform.YouTube;

/// <summary>Typed consumer facade. All calls still require live host grants, consent and channel binding.</summary>
public sealed partial class YouTubeClient(PlatformCapabilityClient platform)
{
    public Task<YouTubePage<Channel>> ReadChannelAsync(CancellationToken ct = default) =>
        platform.InvokeAsync<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel, new(), ct);
    public Task<YouTubePage<Video>> ReadVideoAsync(ReadVideoRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ReadVideoRequest, YouTubePage<Video>>(YouTubeCapabilities.ReadVideo, request, ct);
    public Task<YouTubePage<Playlist>> ListPlaylistsAsync(ListPageRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ListPageRequest, YouTubePage<Playlist>>(YouTubeCapabilities.ListPlaylists, request, ct);
    public Task<YouTubePage<PlaylistItem>> ListPlaylistItemsAsync(ListPlaylistItemsRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ListPlaylistItemsRequest, YouTubePage<PlaylistItem>>(YouTubeCapabilities.ListPlaylistItems, request, ct);
    public Task<YouTubePage<CommentThread>> ListCommentThreadsAsync(ListPageRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ListPageRequest, YouTubePage<CommentThread>>(YouTubeCapabilities.ListCommentThreads, request, ct);
    public Task<YouTubePage<CaptionTrack>> ListCaptionsAsync(ListCaptionsRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ListCaptionsRequest, YouTubePage<CaptionTrack>>(YouTubeCapabilities.ListCaptions, request, ct);
    public Task<YouTubePage<YouTubeComment>> ListCommentRepliesAsync(ListCommentRepliesRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ListCommentRepliesRequest, YouTubePage<YouTubeComment>>(YouTubeCapabilities.ListCommentReplies, request, ct);
    public Task<YouTubePage<YouTubeComment>> ReadCommentAsync(ReadCommentRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ReadCommentRequest, YouTubePage<YouTubeComment>>(YouTubeCapabilities.ReadComment, request, ct);
    /// <summary>Prepare an exact reply for host approval. This method cannot execute provider HTTP.</summary>
    public async Task<ConnectorAction> RequestReplyAsync(ReplyToCommentRequest request, CancellationToken ct = default)
    {
        ValidateReply(request);
        var action = await platform.Connectors.RequestActionAsync(new(YouTubeCapabilities.ReplyToComment,
            JsonSerializer.SerializeToElement(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), request.IdempotencyKey), ct);
        if (action.ActionId == Guid.Empty || action.Capability != YouTubeCapabilities.ReplyToComment)
            throw new InvalidOperationException("The host did not return a reply action receipt.");
        return action;
    }
    public async Task<ConnectorAction> ReadReplyActionAsync(Guid actionId, CancellationToken ct = default)
    {
        var action = await platform.Connectors.ReadActionAsync(new(actionId), ct);
        if (action.ActionId != actionId || action.Capability != YouTubeCapabilities.ReplyToComment)
            throw new InvalidOperationException("This receipt is not for the requested reply action.");
        return action;
    }
    public async Task<ConnectorAction> CancelReplyAsync(Guid actionId, string idempotencyKey, CancellationToken ct = default)
    {
        _ = await ReadReplyActionAsync(actionId, ct);
        var result = await platform.Connectors.CancelActionAsync(new(actionId, idempotencyKey), ct);
        if (result.ActionId != actionId || result.Capability != YouTubeCapabilities.ReplyToComment)
            throw new InvalidOperationException("The cancellation receipt does not match this reply.");
        return result;
    }
    public static void ValidateReply(ReplyToCommentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ParentId) || request.ParentId.Length > 128 ||
            request.ParentId.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not '_' and not '-') ||
            string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 4000 || string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Length > 160 || request.IdempotencyKey.Any(char.IsControl))
            throw new ArgumentException("A top-level comment reference and a reply of at most 4,000 characters are required.", nameof(request));
    }
    public Task<YouTubePage<LiveBroadcast>> ReadLiveBroadcastAsync(ReadLiveBroadcastRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ReadLiveBroadcastRequest, YouTubePage<LiveBroadcast>>(YouTubeCapabilities.ReadLiveBroadcast, request, ct);
    public Task<YouTubePage<LiveStream>> ReadLiveStreamAsync(ReadLiveStreamRequest request, CancellationToken ct = default) =>
        platform.InvokeAsync<ReadLiveStreamRequest, YouTubePage<LiveStream>>(YouTubeCapabilities.ReadLiveStream, request, ct);
    public Task<OfficialAnalytics> ReadAnalyticsAsync(AnalyticsSummaryRequest request, CancellationToken ct = default)
    {
        if (request.EndDate < request.StartDate || request.EndDate.DayNumber - request.StartDate.DayNumber > 366)
            throw new ArgumentException("Choose an ordered date range of at most 367 inclusive days.", nameof(request));
        return platform.InvokeAsync<AnalyticsSummaryRequest, OfficialAnalytics>(YouTubeCapabilities.AnalyticsSummary, request, ct);
    }
}
