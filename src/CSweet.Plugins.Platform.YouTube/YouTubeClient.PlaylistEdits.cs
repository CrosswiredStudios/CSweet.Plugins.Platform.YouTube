using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube;

public sealed record ReadPlaylistRequest(string PlaylistId);
public sealed record PlaylistMetadataChanges(string? Title = null, string? Description = null,
    string? DefaultLanguage = null, bool ClearDefaultLanguage = false);
/// <summary>Complete writable snippet. Status, localizations and playlist contents are not changed.</summary>
public sealed record UpdatePlaylistMetadataRequest(string PlaylistId, string ExpectedETag, string Title,
    string Description, string IdempotencyKey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DefaultLanguage = null);

public sealed partial class YouTubeClient
{
    public Task<YouTubePage<Playlist>> ReadPlaylistAsync(ReadPlaylistRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePlaylistId(request.PlaylistId);
        return platform.InvokeAsync<ReadPlaylistRequest, YouTubePage<Playlist>>(YouTubeCapabilities.ReadPlaylist, request, ct);
    }

    public static UpdatePlaylistMetadataRequest PreparePlaylistMetadataEdit(Playlist before, string confirmedChannel,
        PlaylistMetadataChanges changes, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(before); ArgumentNullException.ThrowIfNull(changes);
        if (string.IsNullOrWhiteSpace(confirmedChannel) ||
            before.Snippet is not { Title: not null, Description: not null } snippet || snippet.ChannelId != confirmedChannel ||
            changes.ClearDefaultLanguage && changes.DefaultLanguage is not null)
            throw new ArgumentException("A complete, owned playlist snapshot and unambiguous changes are required.");
        var request = new UpdatePlaylistMetadataRequest(before.Id, StrongProviderETag(before.Etag),
            changes.Title ?? snippet.Title, changes.Description ?? snippet.Description, idempotencyKey,
            changes.ClearDefaultLanguage ? null : changes.DefaultLanguage ?? snippet.DefaultLanguage);
        ValidatePlaylistMetadataEdit(request);
        if (PlaylistMetadataMatches(before, request))
            throw new ArgumentException("The requested playlist metadata is already present; no change is needed.");
        return request;
    }

    private static void ValidatePlaylistId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || !value.All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '-'))
            throw new ArgumentException("A bounded playlist reference is required.");
    }

    public static void ValidatePlaylistMetadataEdit(UpdatePlaylistMetadataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePlaylistId(request.PlaylistId);
        _ = ConnectorEntityTag.RequireStrong(request.ExpectedETag);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 150 || request.Title.Any(x => char.IsControl(x) || x is '<' or '>') ||
            request.Description is null || Encoding.UTF8.GetByteCount(request.Description) > 5000 || request.Description.Any(x => x is '<' or '>') ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 160 || request.IdempotencyKey.Any(char.IsControl) ||
            request.DefaultLanguage is { } language && (language.Length > 64 || !Regex.IsMatch(language, @"\A[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8}){0,8}\z")))
            throw new ArgumentException("Playlist metadata, language and request key must be valid and bounded.");
    }

    public async Task<ConnectorAction> RequestPlaylistMetadataEditAsync(UpdatePlaylistMetadataRequest request, CancellationToken ct = default)
    {
        ValidatePlaylistMetadataEdit(request);
        var action = await platform.Connectors.RequestActionAsync(new(YouTubeCapabilities.UpdatePlaylistMetadata,
            JsonSerializer.SerializeToElement(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), request.IdempotencyKey), ct);
        if (action.ActionId == Guid.Empty || action.Capability != YouTubeCapabilities.UpdatePlaylistMetadata)
            throw new InvalidOperationException("The returned action is not the requested playlist edit.");
        return action;
    }

    public async Task<ConnectorAction> ReadPlaylistMetadataEditAsync(Guid actionId, CancellationToken ct = default)
    {
        if (actionId == Guid.Empty) throw new ArgumentException("A saved playlist-edit action is required.", nameof(actionId));
        var action = await platform.Connectors.ReadActionAsync(new(actionId), ct);
        if (action.ActionId != actionId || action.Capability != YouTubeCapabilities.UpdatePlaylistMetadata)
            throw new InvalidOperationException("The saved action does not match this playlist edit.");
        return action;
    }

    public async Task<ConnectorAction> CancelPendingPlaylistMetadataEditAsync(Guid actionId, string key, CancellationToken ct = default)
    {
        _ = await ReadPlaylistMetadataEditAsync(actionId, ct);
        var action = await platform.Connectors.CancelActionAsync(new(actionId, key), ct);
        if (action.ActionId != actionId || action.Capability != YouTubeCapabilities.UpdatePlaylistMetadata)
            throw new InvalidOperationException("The cancellation receipt does not match this playlist edit.");
        return action;
    }

    internal static bool PlaylistMetadataMatches(Playlist playlist, UpdatePlaylistMetadataRequest request) =>
        playlist.Id == request.PlaylistId && playlist.Snippet is { } snippet && snippet.Title == request.Title &&
        (snippet.Description ?? "") == request.Description && snippet.DefaultLanguage == request.DefaultLanguage;
}
