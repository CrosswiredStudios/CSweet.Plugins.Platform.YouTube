using CSweet.Agent.SDK;
using CSweet.Plugins.Platform.YouTube;
using Microsoft.Extensions.Hosting;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), CancellationToken.None);
    Console.WriteLine($"Validated {manifest.Id} {manifest.Version}: {manifest.Provides.Count} brokered operations; no provider request was sent.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.AddCSweetAgent<YouTubeConnectorRuntime>();
await builder.Build().RunAsync();
