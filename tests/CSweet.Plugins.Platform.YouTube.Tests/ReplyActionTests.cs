using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class ReplyActionTests
{
    private static readonly ReplyToCommentRequest Expected = new("parent", "Thank you!", "reply-once");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RepeatingRecoveryPagesAndMalformedResultsRemainVisibleReviewStates()
    {
        var id = Guid.NewGuid(); var runtime = Receipt(id, "Indeterminate"); var reads = 0;
        runtime.RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel,
            (_, _) => Task.FromResult(new YouTubePage<Channel>([new("channel", new("Company"))])));
        runtime.RegisterCapability<ListCommentRepliesRequest, YouTubePage<YouTubeComment>>(YouTubeCapabilities.ListCommentReplies, (_, _) =>
        { reads++; return Task.FromResult(new YouTubePage<YouTubeComment>([], "repeated")); });
        var result = await new YouTubeReplyReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.Equal("ReviewRequired", result.Status); Assert.False(result.ScanComplete); Assert.Equal(2, reads);
        var malformed = Receipt(id, "Completed", JsonSerializer.SerializeToElement("invalid result"));
        Assert.Equal("ReviewRequired", (await new YouTubeReplyReconciler(new(malformed.CreateContext().Platform)).InspectAsync(id, Expected)).Status);
    }

    [Fact]
    public async Task ReplyUsesOnlyTheApprovalControlAndExactReviewedPayload()
    {
        var runtime = new AgentTestRuntime().RegisterCapability<RequestConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRequest, (r, _) =>
        {
            Assert.Equal(YouTubeCapabilities.ReplyToComment, r.Capability); Assert.Equal(Expected.IdempotencyKey, r.IdempotencyKey);
            Assert.Equal(Expected, r.Input.Deserialize<ReplyToCommentRequest>(Json));
            return Task.FromResult(new ConnectorAction(Guid.NewGuid(), r.Capability, "AwaitingApproval", DateTimeOffset.UtcNow));
        });
        Assert.Equal("AwaitingApproval", (await new YouTubeClient(runtime.CreateContext().Platform).RequestReplyAsync(Expected)).Status);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => new YouTubeClient(new AgentTestRuntime().CreateContext().Platform).RequestReplyAsync(Expected));
        await Assert.ThrowsAsync<ArgumentException>(() => new YouTubeClient(runtime.CreateContext().Platform).RequestReplyAsync(Expected with { ParentId = "parent.reply" }));
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        Assert.Equal(new[] { YouTubeCapabilities.ReplyToComment, YouTubeCapabilities.UploadVideo, YouTubeCapabilities.UpdateVideoMetadata, YouTubeCapabilities.UpdatePlaylistMetadata }.Order(),
            manifest.ProviderOperations.Where(x => x.Effect != "read").Select(x => x.Capability).Order());
        var operation = Assert.Single(manifest.ProviderOperations, x => x.Capability == YouTubeCapabilities.ReplyToComment);
        Assert.Equal(YouTubeCapabilities.ReplyToComment, operation.Capability);
        Assert.Equal("write", operation.Effect); Assert.Equal("caller-key", operation.Idempotency);
        Assert.Equal("POST", operation.Http!.Method); Assert.Equal(new[] { "content" }, operation.Http.ScopeSets);
        Assert.Equal("/parentId", operation.Http.BodyInputs["/snippet/parentId"]);
        Assert.Equal("/text", operation.Http.BodyInputs["/snippet/textOriginal"]);
        Assert.Equal("/parentId", Assert.Single(operation.Http.ResourceChecks).InputPointer);
        Assert.Equal("/items/0/snippet/channelId", operation.Http.ResourceChecks[0].OwnerPointer);
    }

    [Fact]
    public async Task OnlyAnExactDurableCompletedResponseConfirmsAReply()
    {
        var id = Guid.NewGuid(); var reply = new YouTubeComment("parent.reply", new(TextOriginal: Expected.Text, ParentId: Expected.ParentId));
        var runtime = Receipt(id, "Completed", JsonSerializer.SerializeToElement(reply, Json));
        var result = await new YouTubeReplyReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.Equal("Completed", result.Status); Assert.Equal(reply, result.ConfirmedReply); Assert.Empty(result.PossibleMatches);
        Assert.Equal("ReviewRequired", (await new YouTubeReplyReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id,
            Expected with { Text = "Different reply" })).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UncertainSearchNeverProvesSuccessOrAuthorizesResending(bool matching)
    {
        var id = Guid.NewGuid(); var runtime = Receipt(id, "Indeterminate"); var pages = 0;
        runtime.RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel,
            (_, _) => Task.FromResult(new YouTubePage<Channel>([new("channel", new("Company"))])));
        runtime.RegisterCapability<ListCommentRepliesRequest, YouTubePage<YouTubeComment>>(YouTubeCapabilities.ListCommentReplies, (r, _) =>
        {
            pages++; Assert.Equal(Expected.ParentId, r.ParentId);
            return Task.FromResult(new YouTubePage<YouTubeComment>([
                new("reply-a", new(TextOriginal: matching ? Expected.Text : "Different", ParentId: "parent", AuthorChannelId: new("channel"))),
                new("reply-b", new(TextOriginal: Expected.Text, ParentId: "parent", AuthorChannelId: new("someone-else")))], r.PageToken is null ? "next" : null));
        });
        var result = await new YouTubeReplyReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.Equal("ReviewRequired", result.Status); Assert.Null(result.ConfirmedReply); Assert.True(result.ScanComplete);
        Assert.Equal(matching ? 1 : 0, result.PossibleMatches.Count); Assert.Equal(2, pages);
    }

    [Fact]
    public async Task PendingOrUnavailableRecoveryCannotBecomeCompletion()
    {
        var id = Guid.NewGuid();
        Assert.Equal("AwaitingApproval", (await new YouTubeReplyReconciler(new(Receipt(id, "AwaitingApproval").CreateContext().Platform)).InspectAsync(id, Expected)).Status);
        var uncertain = await new YouTubeReplyReconciler(new(Receipt(id, "Indeterminate").CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.Equal("ReviewRequired", uncertain.Status); Assert.False(uncertain.ScanComplete);
        var wrong = new AgentTestRuntime().RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead,
            (_, _) => Task.FromResult(new ConnectorAction(id, "another.operation", "Completed", DateTimeOffset.UtcNow)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new YouTubeReplyReconciler(new(wrong.CreateContext().Platform)).InspectAsync(id, Expected));
    }

    private static AgentTestRuntime Receipt(Guid id, string status, JsonElement? result = null) => new AgentTestRuntime()
        .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (r, ct) =>
        {
            ct.ThrowIfCancellationRequested(); Assert.Equal(id, r.ActionId);
            return Task.FromResult(new ConnectorAction(id, YouTubeCapabilities.ReplyToComment, status, DateTimeOffset.UtcNow, result));
        });
}
