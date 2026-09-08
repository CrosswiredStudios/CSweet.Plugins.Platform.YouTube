# Connector authority

Playlist metadata consumers separately request `youtube.api.playlist.read.v1` (base consent) and
`youtube.api.playlist.metadata.update.v1` (content consent), plus managed-action request/read/cancel
controls and channel-read authority for reconciliation. The read independently checks ownership.
The edit has a closed snippet-only schema, no channel/URL substitutions, an exact resource-version
precondition and authenticated request/response ownership checks. Host approval or an explicitly
owner-authorized applicable standing policy is still required; content consent alone grants nothing.
Unknown outcomes cannot be retried automatically. Privacy, items, creation and deletion are not part
of this grant. The conversational agent does not yet request the playlist-edit grant.

`YouTubeAnalyticsSnapshot` is a deterministic projection of the existing analytics grant, not another
capability. It rejects unexpected/revenue columns and malformed aggregate values before consuming
agents prepare reports. It does not send requests, calculate replacement metrics or grant authority.

`youtube.api.video-category.list.v1` exposes only the reviewed category catalog read: explicit country
and display language, fixed endpoint/field mask, base scopes and a separate consumer capability grant.
It neither proves channel ownership nor authorizes publishing. Category labels remain untrusted
provider text. Name-based selection requires exactly one assignable match from a complete catalog.

Upload consumers must also hold `communication.chat.read.v1` and supply a retained
`ConversationAttachmentReference` alongside `UploadVideoRequest`. The asset ID must come from that
visible attachment's metadata. The host binds and revalidates organization, employee membership,
conversation, message, attachment, asset and checksum for the entire transfer. Provider consent and
knowing an organization asset ID do not grant access to another conversation's file. Provenance stays
in the host action envelope and is not sent to YouTube. External execution still requires exact approval.

The connector requests no platform tools, employee roles, direct credentials or raw network access.
Administrator approval binds its immutable build, reviewed destinations and deployment OAuth profile.
Each consuming agent also needs an explicit same-organization dependency binding and capability grant.

| Permission set | OAuth scopes (Google auth namespace) | Available operations |
| --- | --- | --- |
| Base | `youtube.readonly`, `yt-analytics.readonly` | Channel, video, playlists, comment threads/replies, official analytics |
| Content | `youtube.force-ssl` | Exactly approved comment replies and conditional video metadata edits, caption metadata and live broadcast/stream status |
| Publishing | `youtube.upload` | Exact-approved organization video uploads through the durable host transfer worker |
| Memberships | `youtube.channel-memberships.creator` | Bounded current-member pages and configured membership levels; independently restricted by Google channel eligibility |
| Partner | `youtubepartner` | Reserved; requires eligibility, content-owner binding and implementation |

Base scopes are requested during setup. Additional consent must be user-initiated and does not
establish API eligibility or grant consumer authority. Setup discovery is bootstrap-only and cannot
be requested through the normal consumer client. Only the narrow comment-reply, video-upload and video-metadata-update operations mutate external state.

Video-edit consumers request `youtube.api.video.metadata.update.v1` separately from the action request,
read and cancel controls. Read-channel and read-video grants supply authenticated evidence and result
checks. The update requires protocol 2.3 and an exact strong version guard. It accepts only the complete
writable snippet, video reference, version and stable action key: no channel, credential, destination,
HTTP method, privacy or scheduling substitution. Existing video reads additionally expose the resource
ETag and default language so a partial edit can preserve other metadata. Empty values are not inferred
from incomplete evidence. Source snapshot text remains untrusted. Conflicts require fresh review;
uncertain mutations remain blocked even if current text happens to match. YouTube Manager requests
this grant for its durable conversational editing workflow; declarations alone never authorize it.

`youtube.api.member.list.v1` and `youtube.api.membership-level.list.v1` use fixed GET mappings with
membership scopes only. The host checks `/items/*/snippet/creatorChannelId` against the confirmed
account before returning records. No request-supplied channel ID is accepted as ownership evidence.
Member reads accept only a bounded continuation token and retain unavailable profiles without guessed
identities; level reads have no pagination input. Neither operation returns profile URLs or allows
changing levels, benefits, prices or membership state. A human owner initiates additional consent in
native settings; missing channel eligibility requires a guided YouTube Partner Manager handoff.
Reply consumers additionally declare `platform.connector.action.request.v1`, `.read.v1` and, when
needed, `.cancel.v1`. These controls and the `youtube.api.comment.reply.v1` operation are independent
grants. The request body contains only the approved parent and reply text; callers cannot supply
another account, token, URL or HTTP method. Indeterminate results remain blocked after read-only
reconciliation; matching text is not an approval, proof of request success or permission to retry.
`youtube.api.comment.read.v1` reads one channel-owned comment by its verified reference. It uses
the API's ID filter without incompatible pagination parameters and returns plain-text fields.
The broker independently verifies the comment's channel before returning it. This read is a
prerequisite for reviewing a reply target; it does not authorize or post a reply.

Upload consumers independently declare `youtube.api.video.upload.v1` and the needed action request,
read and cancel controls. Upload input has no credential, account, URL, chunk offset, arbitrary body
or content-owner substitution. Privacy and audience/disclosure flags are explicit, not inferred
permissions. Read-channel and read-video grants are needed to verify processing and final settings.
`Completed` proves the host saved a provider result; it does not by itself prove public visibility.
The host's durable worker, approved media binding and migration are required. Chat attachment
resolution is implemented; scheduling enforcement remains pending. Do not ask ordinary users for technical IDs.

The live-stream read omits ingestion details and declares the stream-key path secret as a defensive
boundary. No key or upload URL appears in its public result contract. Media mutations and secret
reveal workflows are not implemented by this read slice.
