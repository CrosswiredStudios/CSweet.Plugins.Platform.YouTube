using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class UploadActionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ConversationAttachmentReference Source = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    private static readonly UploadVideoRequest Expected = new(Guid.NewGuid(), "Product launch", "Our new product.", "22",
        "public", false, false, true, "upload-once", ["launch"]);

    [Fact]
    public async Task UploadRequestsOnlyAnExactActionAndTheManifestDeclaresNoRawTransferEscape()
    {
        var runtime = new AgentTestRuntime().RegisterCapability<RequestConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRequest,
            (request, _) =>
            {
                Assert.Equal(YouTubeCapabilities.UploadVideo, request.Capability); Assert.Equal(Expected.IdempotencyKey, request.IdempotencyKey);
                Assert.Equal(Source, request.MediaSource);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(Expected, Json), request.Input));
                Assert.False(request.Input.TryGetProperty("connection", out var connection)); Assert.False(request.Input.TryGetProperty("uploadUrl", out var uploadUrl));
                return Task.FromResult(new ConnectorAction(Guid.NewGuid(), request.Capability, "AwaitingApproval", DateTimeOffset.UtcNow));
            });
        Assert.Equal("AwaitingApproval", (await new YouTubeClient(runtime.CreateContext().Platform).RequestUploadAsync(Expected, Source)).Status);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => new YouTubeClient(new AgentTestRuntime().CreateContext().Platform).RequestUploadAsync(Expected, Source));
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        var operation = Assert.Single(manifest.ProviderOperations, x => x.Capability == YouTubeCapabilities.UploadVideo);
        Assert.Equal("write", operation.Effect); Assert.Equal("caller-key", operation.Idempotency);
        Assert.Equal("POST", operation.Http!.Method); Assert.Equal("https://www.googleapis.com/upload/youtube/v3/videos", operation.Http.Endpoint);
        Assert.Equal(new[] { "publishing" }, operation.Http.ScopeSets); Assert.Equal("/mediaAssetId", operation.Http.MediaInput);
        Assert.Equal("resumable-range.v1", operation.Http.MediaProtocol); Assert.Equal("resumable", operation.Http.QueryConstants["uploadType"]);
        Assert.Equal("/privacyStatus", operation.Http.BodyInputs["/status/privacyStatus"]);
        Assert.Equal("/madeForKids", operation.Http.BodyInputs["/status/selfDeclaredMadeForKids"]);
        Assert.Equal("/containsSyntheticMedia", operation.Http.BodyInputs["/status/containsSyntheticMedia"]);
        var fields = operation.InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("notifySubscribers", fields); Assert.Contains("madeForKids", fields); Assert.Contains("containsSyntheticMedia", fields);
        Assert.False(operation.InputSchema.GetProperty("properties").TryGetProperty("publishAt", out _));
        Assert.False(operation.InputSchema.GetProperty("additionalProperties").GetBoolean());
    }

    [Theory]
    [InlineData("asset")]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("category")]
    [InlineData("privacy")]
    [InlineData("tags")]
    [InlineData("tag-total")]
    [InlineData("key")]
    public void InvalidUploadMetadataIsRejectedBeforeAnActionRequest(string change)
    {
        var request = change switch
        {
            "asset" => Expected with { MediaAssetId = Guid.Empty },
            "title" => Expected with { Title = "<unsafe>" },
            "description" => Expected with { Description = new string('é', 2501) },
            "category" => Expected with { CategoryId = "guess" },
            "privacy" => Expected with { PrivacyStatus = "default" },
            "tags" => Expected with { Tags = ["duplicate", "duplicate"] },
            "tag-total" => Expected with { Tags = Enumerable.Range(0, 6).Select(x => new string((char)('a' + x), 100)).ToArray() },
            _ => Expected with { IdempotencyKey = "unsafe\nkey" }
        };
        Assert.Throws<ArgumentException>(() => YouTubeClient.ValidateUpload(request));
    }

    [Theory]
    [InlineData("uploaded", "Processing")]
    [InlineData("processed", "Processed")]
    [InlineData("failed", "Failed")]
    [InlineData("rejected", "Failed")]
    public async Task ConfirmedBytesAreNotConfusedWithProcessingSuccess(string providerStatus, string expectedStatus)
    {
        var (runtime, id) = Runtime("Completed", ExactVideo(providerStatus));
        var result = await new YouTubeUploadReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.True(result.UploadConfirmed); Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task PrivateRestrictionOrChangedDisclosureIsNotReportedAsRequestedPublicPublication()
    {
        var video = ExactVideo("processed");
        foreach (var actual in new[] { video with { Status = video.Status! with { PrivacyStatus = "private" } },
            video with { Status = video.Status! with { ContainsSyntheticMedia = true } },
            video with { Snippet = video.Snippet! with { Tags = ["changed"] } } })
        {
            var (runtime, id) = Runtime("Completed", actual);
            var result = await new YouTubeUploadReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
            Assert.True(result.UploadConfirmed); Assert.Equal("ReviewRequired", result.Status);
        }
    }

    [Fact]
    public async Task WrongChannelOrMalformedReceiptCannotProveAnUpload()
    {
        var (runtime, id) = Runtime("Completed", ExactVideo("processed") with { Snippet = new("another-channel") });
        var result = await new YouTubeUploadReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.False(result.UploadConfirmed); Assert.Equal("ReviewRequired", result.Status);
        runtime.RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) => Task.FromResult(
            new ConnectorAction(id, YouTubeCapabilities.UploadVideo, "Completed", DateTimeOffset.UtcNow, JsonSerializer.Deserialize<JsonElement>("{\"id\":null}"))));
        result = await new YouTubeUploadReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.False(result.UploadConfirmed); Assert.Equal("ReviewRequired", result.Status);
    }

    [Theory]
    [InlineData("AwaitingApproval")]
    [InlineData("Approved")]
    [InlineData("Executing")]
    [InlineData("Unavailable")]
    [InlineData("Indeterminate")]
    public async Task PendingAndUncertainStatesDoNotReadTheChannelOrStartAnotherUpload(string status)
    {
        var id = Guid.NewGuid();
        var runtime = new AgentTestRuntime().RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead,
            (_, _) => Task.FromResult(new ConnectorAction(id, YouTubeCapabilities.UploadVideo, status, DateTimeOffset.UtcNow)));
        var result = await new YouTubeUploadReconciler(new(runtime.CreateContext().Platform)).InspectAsync(id, Expected);
        Assert.False(result.UploadConfirmed); Assert.Equal(status == "Indeterminate" ? "ReviewRequired" : status, result.Status);
    }

    [Fact]
    public async Task UploadControlCannotReadOrCancelAReplyAction()
    {
        var id = Guid.NewGuid();
        var runtime = new AgentTestRuntime().RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead,
            (_, _) => Task.FromResult(new ConnectorAction(id, YouTubeCapabilities.ReplyToComment, "AwaitingApproval", DateTimeOffset.UtcNow)));
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadUploadActionAsync(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CancelPendingUploadAsync(id, "cancel"));
    }

    private static Video ExactVideo(string status) => new("abcdefghijk", new("channel", Expected.Title, Expected.Description,
        CategoryId: Expected.CategoryId, Tags: Expected.Tags), new(Expected.PrivacyStatus, status,
            SelfDeclaredMadeForKids: Expected.MadeForKids, ContainsSyntheticMedia: Expected.ContainsSyntheticMedia));

    private static (AgentTestRuntime, Guid) Runtime(string status, Video video)
    {
        var id = Guid.NewGuid();
        return (new AgentTestRuntime()
            .RegisterCapability<ReadConnectorAction, ConnectorAction>(PlatformCapabilities.ConnectorActionRead, (_, _) => Task.FromResult(
                new ConnectorAction(id, YouTubeCapabilities.UploadVideo, status, DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(video, Json))))
            .RegisterCapability<ReadChannelRequest, YouTubePage<Channel>>(YouTubeCapabilities.ReadChannel, (_, _) => Task.FromResult(new YouTubePage<Channel>([new("channel", new("Company"))])))
            .RegisterCapability<ReadVideoRequest, YouTubePage<Video>>(YouTubeCapabilities.ReadVideo, (request, _) =>
                { Assert.Equal(video.Id, request.VideoId); return Task.FromResult(new YouTubePage<Video>([video])); }), id);
    }
}
