using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class MembershipTests
{
    private static ChannelMember Member(MemberProfile? profile = null) => new(new("creator", profile,
        new("level-one", "Supporters", ["level-one"], new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 3),
            [new("level-one", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 3)])));

    [Fact]
    public async Task MissingProfilesRemainMembersAndPaginationKeepsTheSameMode()
    {
        var tokens = new List<string?>();
        var runtime = new AgentTestRuntime().RegisterCapability<ListMembersRequest, YouTubePage<ChannelMember>>(YouTubeCapabilities.ListMembers, (r, _) =>
        { tokens.Add(r.PageToken); return Task.FromResult(new YouTubePage<ChannelMember>([Member(), Member(new("member", "Name"))], r.PageToken is null ? "next" : null)); });
        var client = new YouTubeClient(runtime.CreateContext().Platform);
        var page = await client.ListMembersAsync(new());
        Assert.Equal(2, page.Items.Count); Assert.Null(page.Items[0].Snippet.MemberDetails);
        Assert.Null((await client.ListMembersAsync(new(page.NextPageToken))).NextPageToken);
        Assert.Equal(new string?[] { null, "next" }, tokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("page\n")]
    public async Task InvalidPageTokensNeverInvokeAProvider(string token) =>
        await Assert.ThrowsAsync<ArgumentException>(() => new YouTubeClient(new AgentTestRuntime().CreateContext().Platform).ListMembersAsync(new(token)));

    [Fact]
    public async Task EmptyLevelListsAreValidButNotEvidenceOfIneligibility()
    {
        var runtime = new AgentTestRuntime().RegisterCapability<ListMembershipLevelsRequest, YouTubePage<MembershipLevel>>(YouTubeCapabilities.ListMembershipLevels,
            (_, _) => Task.FromResult(new YouTubePage<MembershipLevel>([])));
        Assert.Empty((await new YouTubeClient(runtime.CreateContext().Platform).ListMembershipLevelsAsync()).Items);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("continuation")]
    [InlineData("missing-name")]
    public async Task InvalidLevelResponsesFailClosed(string mode)
    {
        var level = new MembershipLevel("level", new("creator", new(mode == "missing-name" ? "" : "Supporters")));
        var runtime = new AgentTestRuntime().RegisterCapability<ListMembershipLevelsRequest, YouTubePage<MembershipLevel>>(YouTubeCapabilities.ListMembershipLevels,
            (_, _) => Task.FromResult(new YouTubePage<MembershipLevel>(mode == "duplicate" ? [level, level] : [level], mode == "continuation" ? "unexpected" : null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new YouTubeClient(runtime.CreateContext().Platform).ListMembershipLevelsAsync());
    }

    [Fact]
    public async Task InvalidDurationsAndOversizedPagesAreNotReleased()
    {
        var invalid = Member() with { Snippet = Member().Snippet with { MembershipsDetails = Member().Snippet.MembershipsDetails with
            { MembershipsDuration = new(DateTimeOffset.UtcNow, -1) } } };
        foreach (var items in new[] { new[] { invalid }, Enumerable.Repeat(Member(), 26).ToArray() })
        {
            var runtime = new AgentTestRuntime().RegisterCapability<ListMembersRequest, YouTubePage<ChannelMember>>(YouTubeCapabilities.ListMembers,
                (_, _) => Task.FromResult(new YouTubePage<ChannelMember>(items)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => new YouTubeClient(runtime.CreateContext().Platform).ListMembersAsync(new()));
        }
    }

    [Fact]
    public async Task ConsentAndCancellationDoNotFallBackToAnotherGrant()
    {
        var client = new YouTubeClient(new AgentTestRuntime().CreateContext().Platform);
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => client.ListMembersAsync(new()));
        await Assert.ThrowsAsync<PlatformCapabilityException>(() => client.ListMembershipLevelsAsync());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListMembersAsync(new(), cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListMembershipLevelsAsync(cancel.Token));
    }

    [Fact]
    public async Task MembershipMappingsHaveOnlyReviewedScopesAndResponseOwnership()
    {
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        Assert.Equal("2.3", manifest.Protocol.MinimumVersion);
        foreach (var capability in new[] { YouTubeCapabilities.ListMembers, YouTubeCapabilities.ListMembershipLevels })
        {
            var op = manifest.ProviderOperations.Single(x => x.Capability == capability);
            Assert.Equal("read", op.Effect); Assert.Equal("GET", op.Http!.Method);
            Assert.Equal(new[] { "memberships" }, op.Http.ScopeSets);
            Assert.Equal(new[] { "/items/*/snippet/creatorChannelId" }, op.Http.ResponseResourcePointers);
            Assert.Null(op.Http.BoundResourceQuery); Assert.Empty(op.Http.ResourceChecks);
            Assert.DoesNotContain("channelId", op.Http.QueryInputs.Keys);
            Assert.DoesNotContain("profileImageUrl", op.Http.QueryConstants["fields"]);
            Assert.DoesNotContain("channelUrl", op.Http.QueryConstants["fields"]);
        }
        var members = manifest.ProviderOperations.Single(x => x.Capability == YouTubeCapabilities.ListMembers).Http!;
        Assert.Equal("https://www.googleapis.com/youtube/v3/members", members.Endpoint);
        Assert.Equal("all_current", members.QueryConstants["mode"]); Assert.Equal("25", members.QueryConstants["maxResults"]);
        Assert.Equal("/pageToken", members.QueryInputs["pageToken"]);
        var levels = manifest.ProviderOperations.Single(x => x.Capability == YouTubeCapabilities.ListMembershipLevels).Http!;
        Assert.Equal("https://www.googleapis.com/youtube/v3/membershipsLevels", levels.Endpoint);
        Assert.Empty(levels.QueryInputs);
    }
}
