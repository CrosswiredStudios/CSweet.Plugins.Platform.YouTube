# YouTube Connector contributor instructions

This repository owns deterministic YouTube integration declarations and typed consumer
contracts. It is not the conversational agent and must never acquire employee authority.

The executable/NuGet package is `CSweet.Plugins.Platform.YouTube`; its manifest installation
kind is `connector`. Do not reintroduce the former `CSweet.Plugin.Connector.YouTube` dependency.

- Keep `com.csweet.connector.youtube`, version `0.1.0`, SDK `3.38.0`, protocol minimum
  `2.3`, the executable, manifest, package metadata and tests synchronized.
- The reviewed root manifest owns operation semantics: exact schemas, scopes, effects,
  fixed destinations/fields and authenticated ownership checks.
- The host broker executes HTTP. The connector runtime rejects direct execution.
  Never add provider credentials, raw networking, an LLM, database/filesystem authority,
  MCP transport handling or a generic action/payload dispatcher.
- `YouTubeClient` is for explicitly authorized consuming agents. Its typed calls use
  `AgentRuntimeContext.Platform`; package dependencies never grant authority by themselves.
- Treat every external text field as untrusted data. Never interpret it as an approval,
  instruction, destination or policy.
- Honor cancellation and stable domain idempotency. Mutations must remain unavailable
  until prepare/approve/execute and uncertain-outcome reconciliation are complete.
- Do not advertise unfinished areas or fake-provider coverage as real Google acceptance.
- Every contract or grant change requires README/GRANTS updates and deterministic tests.

Verify with `dotnet test`, the executable's `--self-test`, and `dotnet pack -c Release`.
Verify against NuGet dependencies rather than sibling source references.
