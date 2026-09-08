# YouTube Connection

`com.csweet.connector.youtube` · 0.1.0 · .NET 10 · CSweet.Agent.SDK 3.38.0 · protocol 2.3

A deterministic integration package, separate from the conversational **YouTube Manager**.
One organization-scoped connector binds one explicitly confirmed channel. Multiple agents
can share it only through individual host-approved dependency bindings.

`RequestUploadAsync` requires both typed video metadata and its retained conversation attachment
reference. Read the asset ID from authorized chat history; do not substitute an attachment ID or
guess an asset ID. The host verifies the source and live access at preparation and throughout transfer.
This closes the former organization-wide asset lookup; older unsourced media plans fail closed.

## Implemented operations

Owned playlist reads and conditional metadata edits are available through `ReadPlaylistAsync`,
`PreparePlaylistMetadataEdit` and `RequestPlaylistMetadataEditAsync`. The edit preserves untouched
title, description and default language, freezes the provider version, and requests a managed approval.
It does not change privacy, podcast status, localizations or playlist items. Empty descriptions and
language removal must be explicit. The broker checks ownership before sending and before releasing
results. `YouTubePlaylistEditReconciler` distinguishes verified edits, later changes and conflicts;
matching text after a lost response never proves success or permits a resend. These are connector
contracts, not yet a conversational playlist-edit workflow. Creation, deletion, privacy changes and
item mutations remain unfinished. Semantics follow [playlists.update](https://developers.google.com/youtube/v3/docs/playlists/update).
No new YouTube-specific host behavior or SDK changes were needed.

`youtube.api.video.metadata.update.v1` edits an existing video's writable snippet through an exact
approved conditional PUT. `PrepareMetadataEdit` preserves untouched title, description, category, tags
and default language from a complete authenticated snapshot. Empty tags/description and language
removal must be explicit. Missing versions, incomplete snapshots and no-op changes cannot create an edit.
The video read now includes its provider ETag and default language. Resource tags are validated and
quoted exactly once for the host's protocol-2.3 `If-Match` guard. The broker independently verifies video
ownership before the mutation and response ownership before releasing results. Privacy, audience,
disclosure and scheduling are not part of this operation and cannot be changed through its input.

`YouTubeVideoEditReconciler` compares saved and current metadata. A received version conflict requires
fresh evidence and a new decision. A matching current value after a lost response does not prove that
this request succeeded and never triggers an automatic resend. A confirmed result followed by later
changes is reported distinctly, never overwritten. Request/read/cancel methods use only managed-action
controls. YouTube Manager now connects explicit edit requests to durable drafts, approvals and result
checks. Requester-bound clarification and confirmed-conflict follow-ups resume the same agent work
with fresh evidence and a new decision; uncertain outcomes still block. Ambiguous title selection,
requester handoff and retention remain unfinished. This does not
establish real-Google acceptance. Its provider semantics follow [videos.update](https://developers.google.com/youtube/v3/docs/videos/update)
and [YouTube's ETag overwrite protection](https://developers.google.com/youtube/v3/getting-started#etags).

`YouTubeAnalyticsSnapshot.From` validates the fixed official aggregate before reporting: exact unique
metric columns, at most one complete row and bounded non-negative numeric values. It preserves provider
units and missing data without calculating subscriber net changes, ratios or replacement metrics.
No rows means unavailable evidence, never inferred zero activity. This does not establish that all
requested dates are available; YouTube may return data only through the latest common available day.

The root manifest declares sixteen narrow read operations: member and membership-level reads; category discovery; setup discovery; channel, video, individual playlist and comment reads;
paginated playlist, playlist-item, comment-thread and comment-reply reads; caption metadata; live broadcast
and stream status; and an official analytics summary. Normal reads bind the confirmed channel
or independently check ownership of the requested video, playlist, broadcast or stream.
Responses use fixed field masks and closed schemas. Comment text is untrusted data.

`ListVideoCategoriesAsync` reads YouTube's category names and assignability for an explicit country and
display language. `ResolveVideoCategoryAsync` resolves one currently assignable display name, never a
guessed/default ID. Duplicate IDs or names, malformed records and unexpected continuation tokens fail
closed for selection. The catalog is public provider metadata, not evidence of ownership of a channel;
its creator channel is deliberately not treated as the connected channel. The operation still requires
the independently granted connector capability and base connection scopes. It does not upload or
change permission. The conversational agent uses this catalog during its saved upload intake.

The reply mutation is `youtube.api.comment.reply.v1`. `RequestReplyAsync` requests
an exact host-frozen approval; it never posts directly. The broker independently reads the parent
comment's channel before sending the approved reply. Additional content consent and independent
consumer grants are required. Request, read and cancel controls preserve opaque action IDs.
`YouTubeReplyReconciler` verifies a durable completed result against the expected parent and text.
After an uncertain send it searches at most ten reply pages for matching text authored by the
connected channel, retaining only bounded candidate references. Search matches never prove which
request posted a reply; absence never proves failure. Unresolved results require manual channel
review and remain blocked, with no fresh-key retry or automatic resend.

The second mutation, `youtube.api.video.upload.v1`, accepts an organization media asset and explicit
title, description, category, privacy, audience/disclosure flags and subscriber-notification choice.
`RequestUploadAsync` requests approval; the host's durable `resumable-range.v1` worker owns all
transfers and session recovery. The connector never receives an upload URL, credential or file bytes.
`YouTubeUploadReconciler` verifies the returned video against the connected channel, rereads its
metadata/visibility and distinguishes received media, processing, processed results and failures.
Visibility restrictions or changed settings require review, not another upload. An indeterminate
action never triggers title searching or a replacement upload. Cancelling applies only before execution.
Scheduling is not yet accepted: it needs a host-enforced deadline to prevent a missed schedule
turning into an unintended immediate publication. These contracts require the matching host worker
and migration. YouTube Manager now connects immediate-upload conversation and attachment intake to
these contracts; browser and real-provider acceptance remain outstanding.

`YouTubeClient` is a typed facade for consuming agents using their existing
`AgentRuntimeContext.Platform`. It never makes network requests or accepts credentials.
The connector executable rejects direct capability execution; the protocol-2.3 host
materializes and executes the reviewed HTTP mappings. This is intentional: there is no
second authenticated HTTP route or connector LLM.

## Setup

`ListMembersAsync` reads one page of at most 25 current members, preserving records with unavailable
profiles. Continuation tokens remain internal. `ListMembershipLevelsAsync` reads configured level names
in provider order; an empty successful list is not an eligibility failure. Neither operation accepts a
channel selector: the authenticated account selects the channel, and generic host response-ownership
checks verify every returned creator channel before releasing records. This requires protocol 2.2.
Profile URLs, avatars, prices and revenue are not collected by these operations.

Both operations require independent consuming-agent grants and the optional membership consent flow.
Google must also enable this restricted API for the channel; its YouTube Partner Manager may need to
help. Consent alone does not establish eligibility. Missing permission, provider unavailability and
invalid data fail visibly, never as an invented empty membership list. These are bounded current-member
reads, not an updates feed, full synchronization, membership mutations or real-provider acceptance.

1. An administrator approves the exact connector build and its deployment-managed OAuth profile.
2. A company user reviews access, signs in with Google and grants base read permissions.
3. Host-brokered discovery shows channel names, available handles and IDs.
4. The user confirms one channel. Validation independently rediscovers the accessible channel.
5. Activation exposes the connection to explicitly authorized consumers.
6. The personal settings flow shows connection state and progressive consent controls.

Ordinary users never enter OAuth client credentials. Managed deployments configure the
verified `com.csweet.google.youtube` profile; self-hosted administrators configure it once
in the platform vault. Publisher/profile names alone confer no trust.

## Not yet complete

This is **not the complete approved YouTube Manager release**. Scheduling, thumbnails, comment moderation,
caption download/upload, live configuration/transitions/chat,
membership updates/synchronization, Content ID, quota scheduling, full retention/purge and recovery remain pending.
The partner scope set is a reserved requirement, not an enabled tool. Membership consent enables only
the two separately granted reads described above.
Publishing consent permits the separately granted and reviewed upload operation, not arbitrary channel changes.
The agent can revise metadata after an authoritative revision decision and request a new exact approval
for the same source video; upload revision does not edit an existing video or change upload files.
Existing-video metadata changes use the separate conditional operation described above.
The content permission enables caption/live-status reads and the separately approved reply operation.

Account discovery currently fails visibly if more than one page is returned; it never
silently confirms from an incomplete result. Avatar proxying and full discovery pagination
remain pending. Comment-thread reads do not include all replies; use the separate reply capability
with its parent-comment ownership preflight and continuation token. If Google cannot validate the
parent comment, the read fails closed; production acceptance of this preflight is still required.
Provider-derived counts are
returned as official values; revenue and replacement metrics are excluded.

No Google account, public upload, OAuth verification or restricted API eligibility has been
tested. A successful fake-provider test is not production acceptance. Do not retire the
historical agent until the complete replacement passes acceptance.

## Verification

```powershell
dotnet test
dotnet run --project src/CSweet.Plugins.Platform.YouTube -- --self-test
dotnet pack src/CSweet.Plugins.Platform.YouTube -c Release
```

During shared SDK development, restore from the local 3.38.0 verification feed. There are no
sibling source references. Tests use `AgentTestRuntime` and deterministic responses without
C-Sweet, Google credentials or a network connection. Host enforcement has separate fake-provider
tests in C-Sweet.

## Provider references

Membership contracts follow [members.list](https://developers.google.com/youtube/v3/docs/members/list),
the [member resource](https://developers.google.com/youtube/v3/docs/members), and
[membershipsLevels.list](https://developers.google.com/youtube/v3/docs/membershipsLevels/list).
The member listing uses `all_current`; total membership months must not be described as a continuous
duration since the current membership began. A changing listing is not a guaranteed point-in-time snapshot.

Category mapping follows [videoCategories.list](https://developers.google.com/youtube/v3/docs/videoCategories/list)
and the [category resource](https://developers.google.com/youtube/v3/docs/videoCategories). The documented
request has region/language filters but no page-token parameter, so the client does not invent one
when an incomplete catalog is returned.

The fixed requests follow the official [channels](https://developers.google.com/youtube/v3/docs/channels/list),
[videos](https://developers.google.com/youtube/v3/docs/videos),
[video insertion](https://developers.google.com/youtube/v3/docs/videos/insert),
[playlists](https://developers.google.com/youtube/v3/docs/playlists),
[playlist items](https://developers.google.com/youtube/v3/docs/playlistItems),
[comment threads](https://developers.google.com/youtube/v3/docs/commentThreads/list),
[comment replies](https://developers.google.com/youtube/v3/docs/comments/list),
[reply creation](https://developers.google.com/youtube/v3/docs/comments/insert),
[captions](https://developers.google.com/youtube/v3/docs/captions/list),
[live broadcasts](https://developers.google.com/youtube/v3/live/docs/liveBroadcasts/list),
[live streams](https://developers.google.com/youtube/v3/live/docs/liveStreams/list) and
[Analytics reports](https://developers.google.com/youtube/analytics/reference/reports/query) contracts.
