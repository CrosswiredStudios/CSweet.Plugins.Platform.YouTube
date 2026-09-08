using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class VideoEditTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Video Before = new("abcdefghijk", new("channel", "Old title", "Keep description", CategoryId: "22", Tags: ["keep"], DefaultLanguage: "en-US"),
        new("private", PublishAt: "2027-01-01T00:00:00Z"), Etag: "original-version");
    private static UpdateVideoMetadataRequest Edit() => YouTubeClient.PrepareMetadataEdit(Before, "channel", new(Title: "New title"), "edit-once");
    private static Video Updated => Before with { Snippet = Before.Snippet! with { Title = "New title" }, Etag = "new-version" };

    [Fact]
    public void PartialEditPreservesTheCompleteWritableSnippetWithoutStatusFields()
    {
        var edit = Edit();
        Assert.Equal("\"original-version\"", edit.ExpectedETag);
        Assert.Equal("Keep description", edit.Description); Assert.Equal("22", edit.CategoryId);
        Assert.Equal(new[] { "keep" }, edit.Tags); Assert.Equal("en-US", edit.DefaultLanguage);
        var json = JsonSerializer.SerializeToElement(edit, Json);
        Assert.False(json.TryGetProperty("status", out _)); Assert.False(json.TryGetProperty("privacyStatus", out _));
        Assert.False(json.TryGetProperty("publishAt", out _)); Assert.False(json.TryGetProperty("channelId", out _));
        Assert.Equal(edit.ExpectedETag, json.GetProperty("expectedETag").GetString());
    }

    [Fact]
    public void ClearingOptionalFieldsMustBeExplicitAndSourceTagsAreCopied()
    {
        var tags = new List<string> { "original" };
        var snapshot = Before with { Snippet = Before.Snippet! with { Tags = tags } };
        var edit = YouTubeClient.PrepareMetadataEdit(snapshot, "channel", new(Title: "New title"), "key");
        tags[0] = "changed later"; Assert.Equal("original", Assert.Single(edit.Tags));
        var cleared = YouTubeClient.PrepareMetadataEdit(Before, "channel", new(Description: "", Tags: [], ClearDefaultLanguage: true), "key");
        Assert.Empty(cleared.Tags); Assert.Empty(cleared.Description); Assert.Null(cleared.DefaultLanguage);
        Assert.False(JsonSerializer.SerializeToElement(cleared, Json).TryGetProperty("defaultLanguage", out _));
        Assert.Throws<ArgumentException>(() => YouTubeClient.PrepareMetadataEdit(Before, "channel", new(DefaultLanguage: "en", ClearDefaultLanguage: true), "key"));
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("owner")]
    [InlineData("missing-snippet")]
    [InlineData("missing-description")]
    [InlineData("missing-version")]
    public void IncompleteWrongOwnerOrUnchangedSnapshotsCannotPrepareAnEdit(string problem)
    {
        var snapshot = problem switch {
            "owner" => Before with { Snippet = Before.Snippet! with { ChannelId = "another" } },
            "missing-snippet" => Before with { Snippet = null },
            "missing-description" => Before with { Snippet = Before.Snippet! with { Description = null } },
            "missing-version" => Before with { Etag = null }, _ => Before };
        Assert.Throws<ArgumentException>(() => YouTubeClient.PrepareMetadataEdit(snapshot, "channel",
            new(Title: problem == "unchanged" ? "Old title" : "New title"), "key"));
    }

    [Theory]
    [InlineData("etag")]
    [InlineData("video")]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("category")]
    [InlineData("tags")]
    [InlineData("language")]
    [InlineData("key")]
    public void InvalidMetadataFailsBeforeRequestingAuthority(string problem)
    {
        var edit = Edit();
        edit = problem switch {
            "etag" => edit with { ExpectedETag = "*" }, "video" => edit with { VideoId = "https://other" },
            "title" => edit with { Title = "<invalid>" }, "description" => edit with { Description = new string('é', 2501) },
            "category" => edit with { CategoryId = "guess" }, "tags" => edit with { Tags = ["duplicate", "duplicate"] },
            "language" => edit with { DefaultLanguage = "en\r\nHeader: value" }, _ => edit with { IdempotencyKey = "\n" } };
        Assert.Throws<ArgumentException>(() => YouTubeClient.ValidateMetadataEdit(edit));
    }

    [Theory]
    [InlineData("raw-version", "\"raw-version\"")]
    [InlineData("\"already-quoted\"", "\"already-quoted\"")]
    public void ProviderTagIsQuotedExactlyOnce(string value, string expected) => Assert.Equal(expected, YouTubeClient.StrongProviderETag(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("W/\"weak\"")]
    [InlineData("\"one\",\"two\"")]
    [InlineData("raw\r\nvalue")]
    public void MalformedProviderVersionsFailClosed(string? value) => Assert.Throws<ArgumentException>(() => YouTubeClient.StrongProviderETag(value));

    [Fact]
    public async Task EditRequestsAnExactManagedActionAndReviewedMapping()
    {
        var expected = Edit(); var runtime = new AgentTestRuntime().RegisterCapability<RequestConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRequest,
            (request, _) => {
                Assert.Equal(YouTubeCapabilities.UpdateVideoMetadata, request.Capability);
                Assert.Equal(expected.IdempotencyKey, request.IdempotencyKey); Assert.Null(request.MediaSource);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected, Json), request.Input));
                return Task.FromResult(new ConnectorAction(Guid.NewGuid(), request.Capability, "AwaitingApproval", DateTimeOffset.UtcNow));
            });
        Assert.Equal("AwaitingApproval", (await new YouTubeClient(runtime.CreateContext().Platform).RequestMetadataEditAsync(expected)).Status);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => new YouTubeClient(new AgentTestRuntime().CreateContext().Platform).RequestMetadataEditAsync(expected));
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        var operation = Assert.Single(manifest.ProviderOperations, x => x.Capability == YouTubeCapabilities.UpdateVideoMetadata);
        Assert.Equal("2.3", manifest.Protocol.MinimumVersion); Assert.Equal("write", operation.Effect);
        Assert.Equal("caller-key", operation.Idempotency); Assert.Equal("PUT", operation.Http!.Method);
        Assert.Equal("snippet", operation.Http.QueryConstants["part"]); Assert.Equal("/expectedETag", operation.Http.IfMatchInput);
        Assert.Equal(new[] { "content" }, operation.Http.ScopeSets);
        Assert.Equal("/videoId", Assert.Single(operation.Http.ResourceChecks).InputPointer);
        Assert.Equal("/snippet/channelId", Assert.Single(operation.Http.ResponseResourcePointers));
        Assert.DoesNotContain(operation.Http.BodyInputs.Keys, x => x.Contains("status", StringComparison.OrdinalIgnoreCase));
        Assert.False(operation.InputSchema.GetProperty("additionalProperties").GetBoolean());
        var read = Assert.Single(manifest.ProviderOperations, x => x.Capability == YouTubeCapabilities.ReadVideo);
        Assert.Contains("etag", read.Http!.QueryConstants["fields"]); Assert.Contains("defaultLanguage", read.Http.QueryConstants["fields"]);
    }

    [Theory]
    [InlineData("Completed", "same", "Verified", true)]
    [InlineData("Completed", "changed", "ChangedSinceCompletion", true)]
    [InlineData("Completed", "wrong-owner", "ReviewRequired", true)]
    [InlineData("Indeterminate", "same", "ReviewRequired", false)]
    [InlineData("Indeterminate", "changed", "ReviewRequired", false)]
    [InlineData("Blocked", "same", "Conflict", false)]
    [InlineData("AwaitingApproval", "same", "AwaitingApproval", false)]
    public async Task ReconciliationNeverResendsOrMistakesMatchingTextForReceipt(string actionStatus, string currentState, string status, bool confirmed)
    {
        var actionId = Guid.NewGuid(); var reads = 0;
        var current = currentState switch {
            "changed" => Updated with { Snippet = Updated.Snippet! with { Title = "A later human edit" } },
            "wrong-owner" => Updated with { Snippet = Updated.Snippet! with { ChannelId = "another" } }, _ => Updated };
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) =>
                Task.FromResult(new ConnectorAction(actionId, YouTubeCapabilities.UpdateVideoMetadata, actionStatus, DateTimeOffset.UtcNow,
                    actionStatus == "Completed" ? JsonSerializer.SerializeToElement(Updated, Json) : null, "resource_changed")))
            .RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel, (_, _) => {
                reads++; return Task.FromResult(new YouTubePage<Channel>([new("channel", new("Channel"))])); })
            .RegisterCapability<ReadVideoRequest, YouTubePage<Video>>(YouTubeCapabilities.ReadVideo, (request, _) => {
                Assert.Equal(Before.Id, request.VideoId); reads++; return Task.FromResult(new YouTubePage<Video>([current])); });
        var result = await new YouTubeVideoEditReconciler(new(runtime.CreateContext().Platform)).InspectAsync(actionId, Edit(), "channel");
        Assert.Equal(status, result.Status); Assert.Equal(confirmed, result.UpdateConfirmed);
        Assert.Equal(actionStatus is "Completed" or "Indeterminate" ? 2 : 0, reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationChecksTheExactActionAndNeverReportsAWrongReceipt(bool wrongReceipt)
    {
        var id = Guid.NewGuid(); var read = false;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (request, _) => {
                Assert.Equal(id, request.ActionId); read = true;
                return Task.FromResult(new ConnectorAction(id, YouTubeCapabilities.UpdateVideoMetadata, "AwaitingApproval", DateTimeOffset.UtcNow)); })
            .RegisterCapability<CancelConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionCancel, (request, _) => {
                Assert.True(read); Assert.Equal(id, request.ActionId); Assert.Equal("cancel-once", request.IdempotencyKey);
                return Task.FromResult(new ConnectorAction(wrongReceipt ? Guid.NewGuid() : id, YouTubeCapabilities.UpdateVideoMetadata, "Cancelled", DateTimeOffset.UtcNow)); });
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        if (wrongReceipt) await Assert.ThrowsAsync<InvalidOperationException>(() => client.CancelPendingMetadataEditAsync(id, "cancel-once"));
        else Assert.Equal("Cancelled", (await client.CancelPendingMetadataEditAsync(id, "cancel-once")).Status);
    }

    [Fact]
    public async Task CancellationTokenAndEmptyActionAreRespected()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var runtime = new AgentTestRuntime().RegisterCapability<RequestConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRequest,
            (_, ct) => { ct.ThrowIfCancellationRequested(); throw new InvalidOperationException("Cancelled work cannot proceed."); });
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RequestMetadataEditAsync(Edit(), cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadMetadataEditAsync(Guid.Empty));
    }

    [Theory]
    [InlineData("wrong-result")]
    [InlineData("malformed-result")]
    [InlineData("wrong-action")]
    public async Task InvalidReceiptsCannotEstablishSuccess(string problem)
    {
        var actionId = Guid.NewGuid();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) =>
                Task.FromResult(new ConnectorAction(actionId, problem == "wrong-action" ? YouTubeCapabilities.UploadVideo : YouTubeCapabilities.UpdateVideoMetadata,
                    "Completed", DateTimeOffset.UtcNow, problem == "malformed-result" ? JsonSerializer.SerializeToElement("bad") : JsonSerializer.SerializeToElement(Before, Json))))
            .RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel, (_, _) =>
                Task.FromResult(new YouTubePage<Channel>([new("channel", new("Channel"))])));
        var reconciler = new YouTubeVideoEditReconciler(new(runtime.CreateContext().Platform));
        if (problem == "wrong-action") await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.InspectAsync(actionId, Edit(), "channel"));
        else Assert.False((await reconciler.InspectAsync(actionId, Edit(), "channel")).UpdateConfirmed);
    }
}
