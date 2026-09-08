using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSweet.Plugins.Platform.YouTube;

public static class YouTubeCapabilities
{
    public const string DiscoverChannels = "youtube.api.channel.discover.v1";
    public const string ReadChannel = "youtube.api.channel.read.v1";
    public const string ReadVideo = "youtube.api.video.read.v1";
    public const string ListVideoCategories = "youtube.api.video-category.list.v1";
    public const string ListMembers = "youtube.api.member.list.v1";
    public const string ListMembershipLevels = "youtube.api.membership-level.list.v1";
    public const string ListPlaylists = "youtube.api.playlist.list.v1";
    public const string ReadPlaylist = "youtube.api.playlist.read.v1";
    public const string UpdatePlaylistMetadata = "youtube.api.playlist.metadata.update.v1";
    public const string ListPlaylistItems = "youtube.api.playlist-items.list.v1";
    public const string ListCommentThreads = "youtube.api.comment-threads.list.v1";
    public const string ListCommentReplies = "youtube.api.comment-replies.list.v1";
    public const string ReadComment = "youtube.api.comment.read.v1";
    public const string ReplyToComment = "youtube.api.comment.reply.v1";
    public const string UploadVideo = "youtube.api.video.upload.v1";
    public const string UpdateVideoMetadata = "youtube.api.video.metadata.update.v1";
    public const string ListCaptions = "youtube.api.caption.list.v1";
    public const string ReadLiveBroadcast = "youtube.api.live-broadcast.read.v1";
    public const string ReadLiveStream = "youtube.api.live-stream.read.v1";
    public const string AnalyticsSummary = "youtube.api.analytics.summary.v1";
    public static IReadOnlyList<string> All { get; } = [DiscoverChannels, ReadChannel, ReadVideo,
        ListPlaylists, ReadPlaylist, UpdatePlaylistMetadata, ListPlaylistItems, ListCommentThreads, ListCommentReplies, ReadComment, ReplyToComment, UploadVideo, UpdateVideoMetadata, ListCaptions, ReadLiveBroadcast, ReadLiveStream, AnalyticsSummary, ListVideoCategories, ListMembers, ListMembershipLevels];
}

public sealed record ReadChannelRequest;
public sealed record ReadVideoRequest(string VideoId);
public sealed record ListPageRequest([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PageToken = null);
public sealed record ListPlaylistItemsRequest(string PlaylistId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PageToken = null);
public sealed record ListCaptionsRequest(string VideoId);
public sealed record ReadCommentRequest(string CommentId);
public sealed record ReplyToCommentRequest(string ParentId, string Text, string IdempotencyKey);
public sealed record UploadVideoRequest(Guid MediaAssetId, string Title, string Description, string CategoryId,
    string PrivacyStatus, bool MadeForKids, bool ContainsSyntheticMedia, bool NotifySubscribers, string IdempotencyKey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Tags = null);
public sealed record ListCommentRepliesRequest(string ParentId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PageToken = null);
public sealed record ReadLiveBroadcastRequest(string BroadcastId);
public sealed record ReadLiveStreamRequest(string StreamId);
public sealed record AnalyticsSummaryRequest(DateOnly StartDate, DateOnly EndDate);

public sealed record YouTubePage<T>(IReadOnlyList<T> Items, string? NextPageToken = null);
public sealed record Channel(string Id, ChannelSnippet Snippet, ChannelStatistics? Statistics = null, ChannelContentDetails? ContentDetails = null);
public sealed record ChannelSnippet(string Title, string? CustomUrl = null);
public sealed record ChannelStatistics(string? ViewCount = null, string? SubscriberCount = null, string? VideoCount = null, bool? HiddenSubscriberCount = null);
public sealed record ChannelContentDetails(RelatedPlaylists? RelatedPlaylists = null);
public sealed record RelatedPlaylists(string? Uploads = null);
public sealed record Video(string Id, VideoSnippet? Snippet = null, VideoStatus? Status = null,
    VideoStatistics? Statistics = null, VideoContentDetails? ContentDetails = null, string? Etag = null);
public sealed record VideoSnippet(string? ChannelId = null, string? Title = null, string? Description = null,
    string? PublishedAt = null, string? CategoryId = null, IReadOnlyList<string>? Tags = null, string? DefaultLanguage = null);
public sealed record VideoStatus(string? PrivacyStatus = null, string? UploadStatus = null, string? PublishAt = null,
    bool? MadeForKids = null, bool? SelfDeclaredMadeForKids = null, bool? ContainsSyntheticMedia = null);
public sealed record VideoStatistics(string? ViewCount = null, string? LikeCount = null, string? CommentCount = null);
public sealed record VideoContentDetails(string? Duration = null, string? Caption = null);
public sealed record Playlist(string Id, PlaylistSnippet? Snippet = null, PlaylistStatus? Status = null, PlaylistContentDetails? ContentDetails = null, string? Etag = null);
public sealed record PlaylistSnippet(string? ChannelId = null, string? Title = null, string? Description = null, string? DefaultLanguage = null);
public sealed record PlaylistStatus(string? PrivacyStatus = null);
public sealed record PlaylistContentDetails(long? ItemCount = null);
public sealed record PlaylistItem(string Id, PlaylistItemSnippet? Snippet = null, PlaylistItemContentDetails? ContentDetails = null);
public sealed record PlaylistItemSnippet(string? PlaylistId = null, long? Position = null, string? Title = null, VideoReference? ResourceId = null);
public sealed record VideoReference(string? VideoId = null);
public sealed record PlaylistItemContentDetails(string? VideoId = null, string? VideoPublishedAt = null);
public sealed record CommentThread(string Id, CommentThreadSnippet? Snippet = null);
public sealed record CommentThreadSnippet(string? ChannelId = null, string? VideoId = null, long? TotalReplyCount = null,
    bool? CanReply = null, YouTubeComment? TopLevelComment = null);
/// <summary>Every provider-supplied text field is untrusted data, never instructions or approval.</summary>
public sealed record YouTubeComment(string Id, CommentSnippet? Snippet = null);
public sealed record CommentSnippet(string? ChannelId = null, string? VideoId = null, string? TextOriginal = null,
    string? TextDisplay = null, string? AuthorDisplayName = null, long? LikeCount = null, string? PublishedAt = null, string? UpdatedAt = null,
    string? ParentId = null, string? ModerationStatus = null, CommentAuthorChannel? AuthorChannelId = null);
public sealed record CommentAuthorChannel(string Value);
public sealed record CaptionTrack(string Id, CaptionSnippet? Snippet = null);
public sealed record CaptionSnippet(string? VideoId = null, string? Language = null, string? Name = null,
    string? TrackKind = null, bool? IsDraft = null, string? Status = null, string? FailureReason = null, string? LastUpdated = null);
public sealed record LiveBroadcast(string Id, BroadcastSnippet? Snippet = null, BroadcastStatus? Status = null,
    BroadcastContentDetails? ContentDetails = null);
public sealed record BroadcastSnippet(string? ChannelId = null, string? Title = null, string? Description = null,
    string? ScheduledStartTime = null, string? ActualStartTime = null, string? ActualEndTime = null, string? LiveChatId = null);
public sealed record BroadcastStatus(string? LifeCycleStatus = null, string? PrivacyStatus = null, string? RecordingStatus = null,
    bool? MadeForKids = null, bool? SelfDeclaredMadeForKids = null);
public sealed record BroadcastContentDetails(string? BoundStreamId = null, bool? EnableAutoStart = null, bool? EnableAutoStop = null);
public sealed record LiveStream(string Id, PlaylistSnippet? Snippet = null, LiveStreamStatus? Status = null);
public sealed record LiveStreamStatus(string? StreamStatus = null, StreamHealth? HealthStatus = null);
public sealed record StreamHealth(string? Status = null);
public sealed record AnalyticsColumn(string Name, string ColumnType, string DataType);
public sealed record OfficialAnalytics(IReadOnlyList<AnalyticsColumn> ColumnHeaders, IReadOnlyList<IReadOnlyList<JsonElement>>? Rows = null, string? Kind = null);
