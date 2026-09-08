using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record VideoMetadataChanges(string? Title = null, string? Description = null,
    IReadOnlyList<string>? Tags = null, string? CategoryId = null, string? DefaultLanguage = null, bool ClearDefaultLanguage = false);

/// <summary>Complete writable snippet, frozen against one authenticated resource version. No status or scheduling fields.</summary>
public sealed record UpdateVideoMetadataRequest(string VideoId, string ExpectedETag, string Title, string Description,
    string CategoryId, IReadOnlyList<string> Tags, string IdempotencyKey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DefaultLanguage = null);

public sealed partial class YouTubeClient
{
    public static UpdateVideoMetadataRequest PrepareMetadataEdit(Video before, string confirmedChannel,
        VideoMetadataChanges changes, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(before); ArgumentNullException.ThrowIfNull(changes);
        if (string.IsNullOrWhiteSpace(confirmedChannel) || before.Snippet is not { Title: not null, Description: not null, CategoryId: not null } snippet ||
            snippet.ChannelId != confirmedChannel || changes.ClearDefaultLanguage && changes.DefaultLanguage is not null)
            throw new ArgumentException("A complete, owned video snapshot and unambiguous changes are required.");
        var request = new UpdateVideoMetadataRequest(before.Id, StrongProviderETag(before.Etag),
            changes.Title ?? snippet.Title, changes.Description ?? snippet.Description, changes.CategoryId ?? snippet.CategoryId,
            (changes.Tags ?? snippet.Tags ?? []).ToArray(), idempotencyKey,
            changes.ClearDefaultLanguage ? null : changes.DefaultLanguage ?? snippet.DefaultLanguage);
        ValidateMetadataEdit(request);
        if (MetadataMatches(before, request)) throw new ArgumentException("The requested metadata is already present; no change is needed.");
        return request;
    }

    /// <summary>Google returns resource tags as JSON strings; only the exact opaque value is quoted for HTTP.</summary>
    public static string StrongProviderETag(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith("W/", StringComparison.Ordinal))
            throw new ArgumentException("A strong resource version is required.");
        return ConnectorEntityTag.RequireStrong(value.StartsWith('"') ? value : "\"" + value + "\"");
    }

    public static void ValidateMetadataEdit(UpdateVideoMetadataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = ConnectorEntityTag.RequireStrong(request.ExpectedETag);
        if (request.VideoId is not { Length: 11 } || !request.VideoId.All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '-') ||
            string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 100 || request.Title.Any(x => char.IsControl(x) || x is '<' or '>') ||
            request.Description is null || Encoding.UTF8.GetByteCount(request.Description) > 5000 || request.Description.Any(x => x is '<' or '>') ||
            string.IsNullOrWhiteSpace(request.CategoryId) || request.CategoryId.Length > 3 || !request.CategoryId.All(char.IsAsciiDigit) ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 160 || request.IdempotencyKey.Any(char.IsControl) ||
            request.DefaultLanguage is { } language && (language.Length > 64 || !Regex.IsMatch(language, @"\A[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8}){0,8}\z")))
            throw new ArgumentException("The video reference, metadata, language and request key must be valid and bounded.");
        if (request.Tags is not { Count: <= 500 } tags || tags.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500 || x.Any(char.IsControl)) ||
            tags.Distinct(StringComparer.Ordinal).Count() != tags.Count ||
            tags.Sum(x => x.Length + (x.Contains(' ') ? 2 : 0)) + Math.Max(0, tags.Count - 1) > 500)
            throw new ArgumentException("Tags must be unique and fit YouTube's combined 500-character limit.");
    }

    public async Task<ConnectorAction> RequestMetadataEditAsync(UpdateVideoMetadataRequest request, CancellationToken ct = default)
    {
        ValidateMetadataEdit(request);
        var action = await platform.Connectors.RequestActionAsync(new(YouTubeCapabilities.UpdateVideoMetadata,
            JsonSerializer.SerializeToElement(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), request.IdempotencyKey), ct);
        if (action.ActionId == Guid.Empty || action.Capability != YouTubeCapabilities.UpdateVideoMetadata)
            throw new InvalidOperationException("The returned action is not the requested video edit.");
        return action;
    }

    public async Task<ConnectorAction> ReadMetadataEditAsync(Guid actionId, CancellationToken ct = default)
    {
        if (actionId == Guid.Empty) throw new ArgumentException("A saved video-edit action is required.", nameof(actionId));
        var action = await platform.Connectors.ReadActionAsync(new(actionId), ct);
        if (action.ActionId != actionId || action.Capability != YouTubeCapabilities.UpdateVideoMetadata)
            throw new InvalidOperationException("The saved action does not match this video edit.");
        return action;
    }

    public async Task<ConnectorAction> CancelPendingMetadataEditAsync(Guid actionId, string key, CancellationToken ct = default)
    {
        _ = await ReadMetadataEditAsync(actionId, ct);
        var action = await platform.Connectors.CancelActionAsync(new(actionId, key), ct);
        if (action.ActionId != actionId || action.Capability != YouTubeCapabilities.UpdateVideoMetadata)
            throw new InvalidOperationException("The cancellation receipt does not match this video edit.");
        return action;
    }

    internal static bool MetadataMatches(Video video, UpdateVideoMetadataRequest request) =>
        video.Id == request.VideoId && video.Snippet is { } snippet && snippet.Title == request.Title &&
        (snippet.Description ?? "") == request.Description && snippet.CategoryId == request.CategoryId &&
        snippet.DefaultLanguage == request.DefaultLanguage && (snippet.Tags ?? []).Order(StringComparer.Ordinal).SequenceEqual(request.Tags.Order(StringComparer.Ordinal));
}
