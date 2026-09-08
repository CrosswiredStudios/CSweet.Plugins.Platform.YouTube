using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class CategoryTests
{
    [Fact]
    public async Task UsesExactCatalogGrantAndResolvesOnlyAnAssignableDisplayName()
    {
        var runtime = new AgentTestRuntime().RegisterCapability<ListVideoCategoriesRequest, YouTubePage<VideoCategory>>(
            YouTubeCapabilities.ListVideoCategories, (request, ct) =>
            {
                ct.ThrowIfCancellationRequested(); Assert.Equal(new("CA", "fr_CA"), request);
                return Task.FromResult(new YouTubePage<VideoCategory>([new("27", new("Éducation", true)), new("44", new("Bandes-annonces", false))]));
            });
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        var selected = await client.ResolveVideoCategoryAsync(new("CA", "fr_CA"), "éducation");
        Assert.Equal("27", selected.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ResolveVideoCategoryAsync(new("CA", "fr_CA"), "Bandes-annonces"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ResolveVideoCategoryAsync(new("CA", "fr_CA"), "27"));
    }

    [Theory]
    [InlineData("us", "en_US")]
    [InlineData("USA", "en_US")]
    [InlineData("US", "en_US&key=secret")]
    [InlineData("US", "en_US\n")]
    public async Task InvalidInputsNeverInvokeProvider(string region, string language)
    {
        var client = new YouTubeClient(new AgentTestRuntime().CreateContext().Platform);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ListVideoCategoriesAsync(new(region, language)));
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-name")]
    [InlineData("continuation")]
    [InlineData("missing-title")]
    public async Task IncompleteAndAmbiguousCatalogsCannotSelectAnUploadCategory(string mode)
    {
        var items = mode switch
        {
            "duplicate-id" => new VideoCategory[] { new("27", new("Education", true)), new("27", new("Other", true)) },
            "duplicate-name" => [new("27", new("Education", true)), new("28", new("Education", true))],
            "missing-title" => [new("27", new("", true))],
            _ => [new("27", new("Education", true))]
        };
        var runtime = new AgentTestRuntime().RegisterCapability<ListVideoCategoriesRequest, YouTubePage<VideoCategory>>(
            YouTubeCapabilities.ListVideoCategories, (_, _) => Task.FromResult(new YouTubePage<VideoCategory>(items,
                mode == "continuation" ? "not-a-documented-request-parameter" : null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new YouTubeClient(runtime.CreateContext().Platform)
            .ResolveVideoCategoryAsync(new("US"), "Education"));
    }

    [Fact]
    public async Task MissingGrantAndCancellationNeverFallBackToAnotherCapability()
    {
        var client = new YouTubeClient(new AgentTestRuntime().CreateContext().Platform);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => client.ListVideoCategoriesAsync(new("US")));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListVideoCategoriesAsync(new("US"), cancel.Token));
    }

    [Fact]
    public async Task CatalogMappingHasNoMutationOrChannelOwnershipInference()
    {
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        var operation = manifest.ProviderOperations.Single(x => x.Capability == YouTubeCapabilities.ListVideoCategories);
        Assert.Equal("read", operation.Effect);
        Assert.Equal("GET", operation.Http!.Method);
        Assert.Equal("https://www.googleapis.com/youtube/v3/videoCategories", operation.Http.Endpoint);
        Assert.Equal("/regionCode", operation.Http.QueryInputs["regionCode"]);
        Assert.Equal("/language", operation.Http.QueryInputs["hl"]);
        Assert.Equal("snippet", operation.Http.QueryConstants["part"]);
        Assert.Equal("nextPageToken,items(id,snippet(title,assignable))", operation.Http.QueryConstants["fields"]);
        Assert.Null(operation.Http.BoundResourceQuery);
        Assert.Empty(operation.Http.ResourceChecks);
        Assert.DoesNotContain("pageToken", operation.Http.QueryInputs.Keys);
        Assert.False(operation.InputSchema.GetProperty("additionalProperties").GetBoolean());
        Assert.False(operation.OutputSchema.GetProperty("additionalProperties").GetBoolean());
    }
}
