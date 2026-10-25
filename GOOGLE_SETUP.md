# Deployment checklist: Google and YouTube

Development release 0.2.0. These are operator/release tasks, not steps for ordinary channel owners.
The normal experience stays Install → Approve → plugin page → Connect account → Google consent →
import authorized channels → assign business access. No live Google acceptance is recorded here.

## Required evidence before production

1. **Project and APIs.** Use the intended environment's Google Cloud project. Enable YouTube Data
   API and YouTube Analytics API; enable restricted APIs only for implemented, eligible features.
2. **Web client.** Register the server application's OAuth client. Put its client ID and secret only
   in the administrator/deployment vault profile `com.csweet.google.youtube`, not plugin configuration.
3. **Callback.** Register the deployment's exact HTTPS
   `/api/plugin-connections/oauth/callback` address. Verify reverse-proxy/public URL configuration,
   authenticated callback cookies, single-use state, PKCE and rejection of alternate redirects.
4. **Consent and branding.** Configure accurate application identity, support/developer contacts,
   audience and the scopes actually used. Publish applicable homepage/privacy/terms on the operator's
   verified domain. The repository's development notices are not production operator policies.
5. **Scope review.** Start with read-only channel/analytics plus `openid profile` for authenticated
   immutable account identity. Request upload/content/membership scopes only after human feature
   selection. Do not request the reserved partner scope in this release's account permissions.
6. **Verification.** Determine applicable brand/sensitive/restricted scope review or documented
   exceptions. Supply the requested scope justifications and demonstration. Record approval evidence;
   a publisher ID, testing consent or a configured client is not verification.
7. **Testing audience.** Use authorized test accounts and separate development credentials. Record
   consent denial, partial consent, testing-mode refresh behavior and Workspace administrator blocks.
8. **Discovery.** Confirm UserInfo subject/name and actual accessible channel pages. A Google account
   container is not a single all-channel token. Exercise an additional Brand Account authorization;
   never merge accounts on email, names or browser hints, or invent inaccessible channels.
9. **Authority.** Exercise selected-channel and whole-account sharing, explicit future-import
   acknowledgement, independent agent binding and exact mutation approval. Consent alone grants none
   of those. Reconnect must not restore autonomous policies or retarget approvals.
10. **API limits.** Review project quota, upload eligibility and applicable YouTube audits before
    promising public uploads. Membership/partner access requires independently confirmed eligibility;
    unsupported features remain unavailable rather than implied by a consent checkbox.
11. **Data lifecycle.** Verify deletion across inventory, provider-derived inbox/draft/report and
    conversation copies, processors and backups. Record revocation effects, 24-hour deletion target,
    seven-day maximum and applicable 30-day refresh/deletion. Content-free audit is distinct from
    retained provider data. Do not claim deletion from merely queuing a job.
12. **Release acceptance.** Run the real deployed install/connect/reconnect/import/grant/agent/action/
    revoke/disconnect journey with explicit authorization for any mutation. Save evidence without
    credentials, consent codes or private channel content; then approve the actual package digests.

## Gates still open

- Real-Google and PostgreSQL multi-process concurrency acceptance have not been performed.
- Complete provenance-aware deletion and periodic account/resource lifecycle enforcement are unfinished.
- Policy URLs in this development package point to repository notices. Production needs reviewed
  declarations matching the operator's published policies; do not treat these notices as that setup.
- The native browser harness uses a synthetic provider and simulates package installation. It is not
  evidence of Google verification, public-upload eligibility or restricted-feature access.

## Authoritative guidance

- [Google web-server authorization](https://developers.google.com/youtube/data/guides/auth/server-side-web-apps)
  and [OpenID Connect/UserInfo](https://developers.google.com/identity/openid-connect/openid-connect).
- [Google scope and application verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification).
- [YouTube Developer Policies](https://developers.google.com/youtube/terms/developer-policies).
- [YouTube upload requirements](https://developers.google.com/youtube/v3/docs/videos/insert).
