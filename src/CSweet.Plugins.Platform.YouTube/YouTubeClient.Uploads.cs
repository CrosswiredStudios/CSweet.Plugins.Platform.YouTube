using System.Text;
using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube;

public sealed partial class YouTubeClient
{
    /// <summary>Requests an exact approved upload. The host owns the session URL, chunks, credentials and recovery.</summary>
    public async Task<ConnectorAction> RequestUploadAsync(UploadVideoRequest request,
        ConversationAttachmentReference mediaSource, CancellationToken ct = default)
    {
        ValidateUpload(request);
        ArgumentNullException.ThrowIfNull(mediaSource);
        var action = await platform.Connectors.RequestActionAsync(new(YouTubeCapabilities.UploadVideo,
            JsonSerializer.SerializeToElement(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), request.IdempotencyKey)
            { MediaSource = mediaSource }, ct);
        if (action.ActionId == Guid.Empty || action.Capability != YouTubeCapabilities.UploadVideo)
            throw new InvalidOperationException("The host did not return an upload action receipt.");
        return action;
    }

    public async Task<ConnectorAction> ReadUploadActionAsync(Guid actionId, CancellationToken ct = default)
    {
        var action = await platform.Connectors.ReadActionAsync(new(actionId), ct);
        if (action.ActionId != actionId || action.Capability != YouTubeCapabilities.UploadVideo)
            throw new InvalidOperationException("This receipt is not for the requested upload.");
        return action;
    }

    /// <summary>Closes an unstarted upload only. It cannot undo an in-flight transfer or uploaded video.</summary>
    public async Task<ConnectorAction> CancelPendingUploadAsync(Guid actionId, string idempotencyKey, CancellationToken ct = default)
    {
        _ = await ReadUploadActionAsync(actionId, ct);
        var result = await platform.Connectors.CancelActionAsync(new(actionId, idempotencyKey), ct);
        if (result.ActionId != actionId || result.Capability != YouTubeCapabilities.UploadVideo)
            throw new InvalidOperationException("The cancellation receipt does not match this upload.");
        return result;
    }

    public static void ValidateUpload(UploadVideoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MediaAssetId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 100 ||
            request.Title.Any(x => char.IsControl(x) || x is '<' or '>') || request.Description is null ||
            Encoding.UTF8.GetByteCount(request.Description) > 5000 || request.Description.Any(x => x is '<' or '>') ||
            string.IsNullOrWhiteSpace(request.CategoryId) || request.CategoryId.Length > 3 || !request.CategoryId.All(char.IsAsciiDigit) ||
            request.PrivacyStatus is not ("private" or "unlisted" or "public") ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 160 || request.IdempotencyKey.Any(char.IsControl))
            throw new ArgumentException("Choose an organization video, bounded metadata, an explicit privacy setting and a stable request key.", nameof(request));
        if (request.Tags is { } tags && (tags.Count > 500 || tags.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500 || x.Any(char.IsControl)) ||
            tags.Distinct(StringComparer.Ordinal).Count() != tags.Count ||
            tags.Sum(x => x.Length + (x.Contains(' ') ? 2 : 0)) + Math.Max(0, tags.Count - 1) > 500))
            throw new ArgumentException("Video tags must be unique and fit YouTube's combined 500-character limit.", nameof(request));
    }
}
