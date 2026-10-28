using CSweet.Agent.SDK;
using CSweet.Plugins.Platform.YouTube;

var runtime = new YouTubeConnectorRuntime();
if (runtime.AgentId != "com.csweet.connector.youtube" || YouTubeCapabilities.All.Count != 21)
    throw new InvalidOperationException("The NuGet consumer resolved an unexpected connector.");
// Compile against the public facade and SDK without either repository's project references.
YouTubeClient CreateClient(AgentRuntimeContext context) => new(context.Platform);
_ = (Func<AgentRuntimeContext, YouTubeClient>)CreateClient;
Console.WriteLine($"NuGet-only consumer: {runtime.AgentId} {runtime.Version}; {YouTubeCapabilities.All.Count} capabilities.");
