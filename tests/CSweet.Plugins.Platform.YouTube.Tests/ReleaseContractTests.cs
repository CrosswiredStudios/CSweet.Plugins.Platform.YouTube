using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

namespace CSweet.Plugins.Platform.YouTube.Tests;

public sealed class ReleaseContractTests
{
    [Fact]
    public void SourceManifestBuildAssemblyAndNotesUseTheSameVersion()
    {
        var root = Root();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "csweet-plugin.json")));
        var version = manifest.RootElement.GetProperty("version").GetString();
        var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.Equal(version, props.Descendants("Version").Single().Value);
        Assert.Equal(version, new YouTubeConnectorRuntime().Version);
        var assembly = typeof(YouTubeClient).Assembly;
        Assert.Equal(version, assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0]);
        Assert.Equal($"# {version}", File.ReadLines(Path.Combine(root, "releases", version + ".md")).First());
    }

    [Fact]
    public void PackageConsumerHasNoProjectReferenceAndRequiresAnExactCandidateVersion()
    {
        var consumer = XDocument.Load(Path.Combine(Root(), "tests", "PackageConsumer", "PackageConsumer.csproj"));
        Assert.Empty(consumer.Descendants("ProjectReference"));
        var reference = Assert.Single(consumer.Descendants("PackageReference"));
        Assert.Equal("CSweet.Plugins.Platform.YouTube", reference.Attribute("Include")!.Value);
        Assert.Equal("[$(ConnectorPackageVersion)]", reference.Attribute("Version")!.Value);
    }

    [Fact]
    public void CandidateSourceMappingCannotResolveConnectorFromAnotherFeed()
    {
        var config = XDocument.Load(Path.Combine(Root(), "tests", "PackageConsumer", "NuGet.Config"));
        var sources = config.Descendants("packageSource").ToArray();
        var candidate = Assert.Single(sources, s => s.Attribute("key")!.Value == "candidate");
        Assert.Equal("CSweet.Plugins.Platform.YouTube", Assert.Single(candidate.Elements("package")).Attribute("pattern")!.Value);
        Assert.DoesNotContain(sources.Where(s => s != candidate).SelectMany(s => s.Elements("package")),
            p => p.Attribute("pattern")!.Value == "CSweet.Plugins.Platform.YouTube");
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
