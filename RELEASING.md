# Releasing the YouTube connector

nuget.org is the authoritative public feed for the typed consumer package
`CSweet.Plugins.Platform.YouTube`. The GitHub source repository is the enterprise plugin installation
source. These are different delivery paths: a local `.nupkg` does not make an agent dependency
available in CI, and a NuGet publication does not install or authorize a plugin in C-Sweet.

## Verify locally

Run `pwsh scripts/Test-Release.ps1` using .NET 10. It restores from nuget.org, runs all connector tests
and the executable self-test, packs the version in `csweet-plugin.json`, inspects NuGet metadata and
the exact embedded manifest, and compiles/runs `tests/PackageConsumer` using a fresh package cache.
The consumer has no project references. Its source mapping forces the connector to come from the
just-built candidate, with transitive dependencies from nuget.org.

`artifacts/release/<version>` contains the package, reviewed manifest, release notes and SHA-256
checksums. It is verification output only. The host executes reviewed connector declarations;
these packages do not need an agent VM, compiled runtime bundle or scheduling configuration.

## Publish a reviewed candidate

1. An authorized maintainer must own or obtain permission for this NuGet package ID.
2. Configure a NuGet trusted publisher for account `CrosswiredStudios`, repository
   `CrosswiredStudios/CSweet.Plugins.Platform.YouTube`, workflow `connector-release.yml`, environment
   `release`, and only package ID `CSweet.Plugins.Platform.YouTube` (create/push for first publication).
   Configure that GitHub environment with required owner review, no administrator bypass, and only
   the exact `main` branch, with no tag rule. Self-review is allowed so the sole owner can approve
   a manual run. No long-lived NuGet key or OAuth credential belongs in CI.
3. Synchronize the manifest, runtime, build/package version, contributor instructions and release
   notes. Preserve historical release notes. Do not replace an already published package version.
4. Push the reviewed code through the normal repository review process. Every PR/main push verifies;
   it does not publish. Inspect the test results and `connector-release` artifact.
5. After the NuGet policy and reviewed workflow are ready, run `connector-release.yml` on the reviewed
   `main` revision with `publish` selected and approve the protected `release` job. The verification
   job uploads an immutable artifact; publishing downloads that exact artifact ID and verifies its
   closed file list, checksums, package ID/version, clean source commit and NuGet repository metadata
   before `NuGet/login@v1` requests a temporary key. Only the publish job has `id-token: write`.
   No tag or GitHub release is created. The default manual run verifies without publishing.
6. Confirm the exact version is indexed on nuget.org with a clean package-only restore. If a rerun
   encounters an existing version, fail and inspect it; never silently skip a conflicting package.
   Preserve the immutable artifact and evidence before investigating any uncertain publication outcome.
7. In the consuming agent's separate release, pin this verified package version, keep its dependency
   manifest range compatible, and run its clean restore/build/tests. This connector change does not
   edit or release the agent. Historical `0.2.0` references are not proof that version was published.

## Accept the installed plugin separately

Install the exact reviewed repository commit through C-Sweet's plugin review. Verify the broker-only
declaration digest, then follow `GOOGLE_SETUP.md` with an explicitly authorized test channel:
Connect, provider consent, discovery, narrow business sharing, independent agent binding, an approved
operation when authorized, reconnect, disconnect and recorded cleanup evidence. Preserve credentials
and private channel content outside release logs. Missing physical copy receipts remain pending.

The local customer-owned OAuth mode can be accepted independently. The proposed C-Sweet-managed
connection service is not implemented by this release. Production policies, OAuth verification,
database/cleanup deployment and real-provider acceptance remain separate prerequisites.
