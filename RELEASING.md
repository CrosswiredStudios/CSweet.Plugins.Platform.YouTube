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
2. Create a GitHub environment named `release`, restrict it to reviewed release refs and configure
   required reviewers. Give its `NUGET_API_KEY` secret a scoped, expiring NuGet push key for this ID.
   No OAuth credential belongs in CI. Prefer federated NuGet publishing when the organization has
   configured and validated that separately; this workflow does not assume it already exists.
3. Synchronize the manifest, runtime, build/package version, contributor instructions and release
   notes. Preserve historical release notes. Do not replace an already published package version.
4. Push the reviewed code through the normal repository review process. Every PR/main push verifies;
   it does not publish. Inspect the test results and `connector-release` artifact.
5. When authorized, tag that exact commit `v<version>`, or run the workflow on main with `publish`
   selected. The protected job verifies artifact checksums, pushes the NuGet package and creates a
   GitHub preview release at that commit. No publication has occurred until those operations succeed.
6. Confirm the exact version is indexed on nuget.org with a clean package-only restore. If a rerun
   encounters an existing version, fail and inspect it; never silently skip a conflicting package.
   Recover a partially completed GitHub release only after comparing its package and commit evidence.
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
