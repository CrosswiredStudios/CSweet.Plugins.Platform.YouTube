using CSweet.Agent.SDK;

namespace CSweet.Plugins.Platform.YouTube;

/// <summary>Fail-closed runtime entry point. Reviewed HTTP mappings execute only in the host broker.</summary>
public sealed class YouTubeConnectorRuntime : CSweetAgentBase
{
    public override string AgentId => "com.csweet.connector.youtube";
    public override string Version => "0.2.1";

    protected override Task<AgentWorkResult> ExecuteCapabilityCoreAsync(AgentCapabilityRequest request,
        AgentRuntimeContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AgentWorkResult.Failure(
            "Connector operations require the protocol 2.4 host broker, reviewed provider access, business resource grants and an explicit consumer binding."));
    }
}
