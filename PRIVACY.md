# YouTube integration privacy notice — development release 0.2.0

This connector declares account identity, channel discovery and the supported YouTube operations.
C-Sweet, not this package or an agent, stores encrypted OAuth credentials and brokers requests.
The `openid profile` scopes identify the authorization using an immutable subject and optional name;
email is not an identity key and is not requested. Channel metadata is imported only from authorized
provider responses. Connecting does not grant any business or agent access automatically.

Business grants, agent bindings and action approvals are independent. Provider text is untrusted
data. It must not change instructions, destinations or permissions. Authorized data must not be used
for general model training, advertising or unrelated profiling. A deployment must disclose and review
its model/processors before enabling provider-data processing.

Disconnect must immediately stop access and queue provider revocation and deletion of authorized
copies. This package declares a 24-hour deletion target and seven-day maximum, plus applicable
30-day metadata refresh/deletion. Declaring these values is not proof that a deployment has completed
purging conversations, backups or processor copies. Content-free security audit records may remain.

Before production, the deployment operator must publish its actual privacy policy, processor list,
retention controls and contact/deletion procedure on its verified public domain, configure matching
OAuth branding and satisfy applicable Google/YouTube policies. This development notice is not a
production compliance certification or a replacement for that operator policy.

You can also review or remove access in [Google account permissions](https://myaccount.google.com/permissions).
See [Google's Privacy Policy](https://policies.google.com/privacy).
