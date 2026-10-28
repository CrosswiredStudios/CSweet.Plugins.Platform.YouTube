using CSweet.Agent.SDK;
using CSweet.Plugins.Platform.YouTube;
using Microsoft.Extensions.Hosting;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), CancellationToken.None);
    var runtime = new YouTubeConnectorRuntime();
    if (manifest.Id != runtime.AgentId || manifest.Version != runtime.Version || manifest.Kind != "connector" ||
        !manifest.Capabilities.Order(StringComparer.Ordinal).SequenceEqual(YouTubeCapabilities.All.Order(StringComparer.Ordinal)))
        throw new InvalidOperationException("The packaged connector identity or capabilities do not match its implementation.");
    Console.WriteLine($"Validated {manifest.Id} {manifest.Version}: {manifest.Provides.Count} brokered operations; no provider request was sent.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.AddCSweetAgent<YouTubeConnectorRuntime>();
await builder.Build().RunAsync();
