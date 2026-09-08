using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class PlaylistEditTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Playlist Before = new("PL_example", new("channel", "Old title", "Keep description", "en-US"),
        new("private"), new(4), "old-version");
    private static Playlist Updated => Before with { Snippet = Before.Snippet! with { Title = "New title" }, Etag = "new-version" };
    private static UpdatePlaylistMetadataRequest Edit() => YouTubeClient.PreparePlaylistMetadataEdit(Before, "channel", new(Title: "New title"), "edit-once");

    [Fact]
    public void PartialChangesPreserveTheWritableSnippetAndExcludeOtherParts()
    {
        var edit = Edit();
        Assert.Equal("\"old-version\"", edit.ExpectedETag);
        Assert.Equal("Keep description", edit.Description); Assert.Equal("en-US", edit.DefaultLanguage);
        var input = JsonSerializer.SerializeToElement(edit, Json);
        Assert.Equal(new[] { "playlistId", "expectedETag", "title", "description", "idempotencyKey", "defaultLanguage" }.Order(),
            input.EnumerateObject().Select(x => x.Name).Order());
        var clear = YouTubeClient.PreparePlaylistMetadataEdit(Before, "channel", new(Description: "", ClearDefaultLanguage: true), "clear");
        Assert.Empty(clear.Description); Assert.Null(clear.DefaultLanguage);
        Assert.False(JsonSerializer.SerializeToElement(clear, Json).TryGetProperty("defaultLanguage", out _));
        Assert.Throws<ArgumentException>(() => YouTubeClient.PreparePlaylistMetadataEdit(Before, "channel", new(DefaultLanguage: "fr", ClearDefaultLanguage: true), "key"));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("description")]
    [InlineData("snippet")]
    [InlineData("version")]
    [InlineData("unchanged")]
    public void UnsafeOrUnnecessarySnapshotsCannotCreateWork(string problem)
    {
        var before = problem switch {
            "owner" => Before with { Snippet = Before.Snippet! with { ChannelId = "other" } },
            "description" => Before with { Snippet = Before.Snippet! with { Description = null } },
            "snippet" => Before with { Snippet = null }, "version" => Before with { Etag = null }, _ => Before };
        Assert.Throws<ArgumentException>(() => YouTubeClient.PreparePlaylistMetadataEdit(before, "channel",
            new(Title: problem == "unchanged" ? "Old title" : "New title"), "key"));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("etag")]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("language")]
    [InlineData("key")]
    public async Task InvalidInputFailsBeforeRequestingAuthority(string problem)
    {
        var request = problem switch {
            "id" => Edit() with { PlaylistId = "PL_example\n" }, "etag" => Edit() with { ExpectedETag = "*" },
            "title" => Edit() with { Title = new string('a', 151) }, "description" => Edit() with { Description = new string('é', 2501) },
            "language" => Edit() with { DefaultLanguage = "en\r\nHeader: value" }, _ => Edit() with { IdempotencyKey = "\n" } };
        await Assert.ThrowsAsync<ArgumentException>(() => new YouTubeClient(new AgentTestRuntime().CreateContext().Platform).RequestPlaylistMetadataEditAsync(request));
    }

    [Fact]
    public async Task ManifestHasExactOwnedReadAndConditionalSnippetOnlyMutation()
    {
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        var edit = Assert.Single(manifest.ProviderOperations, x => x.Capability == YouTubeCapabilities.UpdatePlaylistMetadata);
        Assert.Equal("write", edit.Effect); Assert.Equal("caller-key", edit.Idempotency);
        Assert.Equal("PUT", edit.Http!.Method); Assert.Equal("/expectedETag", edit.Http.IfMatchInput);
        Assert.Equal(new[] { "content" }, edit.Http.ScopeSets);
        Assert.Equal("https://www.googleapis.com/youtube/v3/playlists", edit.Http.Endpoint);
        Assert.Equal("snippet", edit.Http.QueryConstants["part"]);
        Assert.Equal(new[] { "/id", "/snippet/title", "/snippet/description", "/snippet/defaultLanguage" }.Order(), edit.Http.BodyInputs.Keys.Order());
        Assert.Equal("/playlistId", Assert.Single(edit.Http.ResourceChecks).InputPointer);
        Assert.Equal("/items/0/snippet/channelId", edit.Http.ResourceChecks[0].OwnerPointer);
        Assert.Equal("/snippet/channelId", Assert.Single(edit.Http.ResponseResourcePointers));
        Assert.False(edit.InputSchema.GetProperty("additionalProperties").GetBoolean());
        var read = Assert.Single(manifest.ProviderOperations, x => x.Capability == YouTubeCapabilities.ReadPlaylist);
        Assert.Equal("read", read.Effect); Assert.Equal("GET", read.Http!.Method);
        Assert.Equal(new[] { "base" }, read.Http.ScopeSets);
        Assert.Equal("/playlistId", read.Http.QueryInputs["id"]);
        Assert.False(read.Http.QueryConstants.ContainsKey("maxResults"));
        Assert.Equal("/playlistId", Assert.Single(read.Http.ResourceChecks).InputPointer);
        Assert.Contains("etag", read.Http.QueryConstants["fields"]);
        Assert.Contains("defaultLanguage", read.Http.QueryConstants["fields"]);
    }

    [Fact]
    public async Task ClientRequestsApprovalWithStableExactInputAndRequiresAGrant()
    {
        var expected = Edit();
        var runtime = new AgentTestRuntime().RegisterCapability<RequestConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRequest,
            (request, _) => {
                Assert.Equal(YouTubeCapabilities.UpdatePlaylistMetadata, request.Capability); Assert.Null(request.MediaSource);
                Assert.Equal(expected.IdempotencyKey, request.IdempotencyKey);
                Assert.Equal(expected, request.Input.Deserialize<UpdatePlaylistMetadataRequest>(Json));
                return Task.FromResult(new ConnectorAction(Guid.NewGuid(), request.Capability, "AwaitingApproval", DateTimeOffset.UtcNow)); });
        Assert.Equal("AwaitingApproval", (await new YouTubeClient(runtime.CreateContext().Platform).RequestPlaylistMetadataEditAsync(expected)).Status);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => new YouTubeClient(new AgentTestRuntime().CreateContext().Platform).RequestPlaylistMetadataEditAsync(expected));
    }

    [Theory]
    [InlineData("Completed", "same", "Verified", true)]
    [InlineData("Completed", "changed", "ChangedSinceCompletion", true)]
    [InlineData("Completed", "owner", "ReviewRequired", true)]
    [InlineData("Completed", "missing", "ReviewRequired", true)]
    [InlineData("Completed", "page", "ReviewRequired", true)]
    [InlineData("Indeterminate", "same", "ReviewRequired", false)]
    [InlineData("Indeterminate", "changed", "ReviewRequired", false)]
    [InlineData("Blocked", "same", "Conflict", false)]
    [InlineData("AwaitingApproval", "same", "AwaitingApproval", false)]
    [InlineData("Rejected", "same", "Rejected", false)]
    public async Task ReconcileNeverResendsOrUsesCurrentTextAsProof(string actionStatus, string state, string expectedStatus, bool confirmed)
    {
        var id = Guid.NewGuid(); var reads = 0;
        var current = state switch {
            "changed" => Updated with { Snippet = Updated.Snippet! with { Title = "Later human edit" } },
            "owner" => Updated with { Snippet = Updated.Snippet! with { ChannelId = "another" } }, _ => Updated };
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) =>
                Task.FromResult(new ConnectorAction(id, YouTubeCapabilities.UpdatePlaylistMetadata, actionStatus, DateTimeOffset.UtcNow,
                    actionStatus == "Completed" ? JsonSerializer.SerializeToElement(Updated, Json) : null, "resource_changed")))
            .RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel, (_, _) => {
                reads++; return Task.FromResult(new YouTubePage<Channel>([new("channel", new("Channel"))])); })
            .RegisterCapability<ReadPlaylistRequest, YouTubePage<Playlist>>(YouTubeCapabilities.ReadPlaylist, (request, _) => {
                Assert.Equal(Before.Id, request.PlaylistId); reads++;
                return Task.FromResult(new YouTubePage<Playlist>(state == "missing" ? [] : [current], state == "page" ? "unexpected" : null)); });
        var result = await new YouTubePlaylistEditReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Edit(), "channel");
        Assert.Equal(expectedStatus, result.Status); Assert.Equal(confirmed, result.UpdateConfirmed);
        Assert.Equal(actionStatus is "Completed" or "Indeterminate" ? 2 : 0, reads);
    }

    [Theory]
    [InlineData("wrong-result")]
    [InlineData("malformed")]
    [InlineData("wrong-action")]
    [InlineData("wrong-id")]
    [InlineData("wrong-channel")]
    public async Task InvalidReceiptsOrConnectionCannotEstablishSuccess(string problem)
    {
        var id = Guid.NewGuid();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) =>
                Task.FromResult(new ConnectorAction(problem == "wrong-id" ? Guid.NewGuid() : id,
                    problem == "wrong-action" ? YouTubeCapabilities.UploadVideo : YouTubeCapabilities.UpdatePlaylistMetadata,
                    "Completed", DateTimeOffset.UtcNow, problem == "malformed" ? JsonSerializer.SerializeToElement("bad") : JsonSerializer.SerializeToElement(Before, Json))))
            .RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel, (_, _) =>
                Task.FromResult(new YouTubePage<Channel>([new(problem == "wrong-channel" ? "other" : "channel", new("Channel"))])));
        var reconciler = new YouTubePlaylistEditReconciler(new(runtime.CreateContext().Platform));
        if (problem is "wrong-action" or "wrong-id") await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.InspectAsync(id, Edit(), "channel"));
        else Assert.False((await reconciler.InspectAsync(id, Edit(), "channel")).UpdateConfirmed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationReadsExactActionAndValidatesTheReturnedReceipt(bool wrongReceipt)
    {
        var id = Guid.NewGuid(); var read = false;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) => {
                read = true; return Task.FromResult(new ConnectorAction(id, YouTubeCapabilities.UpdatePlaylistMetadata, "AwaitingApproval", DateTimeOffset.UtcNow)); })
            .RegisterCapability<CancelConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionCancel, (request, _) => {
                Assert.True(read); Assert.Equal(id, request.ActionId); Assert.Equal("cancel-once", request.IdempotencyKey);
                return Task.FromResult(new ConnectorAction(wrongReceipt ? Guid.NewGuid() : id, YouTubeCapabilities.UpdatePlaylistMetadata, "Cancelled", DateTimeOffset.UtcNow)); });
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        if (wrongReceipt) await Assert.ThrowsAsync<InvalidOperationException>(() => client.CancelPendingPlaylistMetadataEditAsync(id, "cancel-once"));
        else Assert.Equal("Cancelled", (await client.CancelPendingPlaylistMetadataEditAsync(id, "cancel-once")).Status);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadPlaylistMetadataEditAsync(Guid.Empty));
    }

    [Fact]
    public async Task CancellationAndInvalidReadReferencesFailBeforeWork()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var runtime = new AgentTestRuntime().RegisterCapability<RequestConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRequest,
            (_, ct) => { ct.ThrowIfCancellationRequested(); throw new InvalidOperationException(); });
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RequestPlaylistMetadataEditAsync(Edit(), cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadPlaylistAsync(new("https://other")));
    }
}
