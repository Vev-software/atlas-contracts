# Landscape share digest — the minimized sharing surface

The **landscape share digest** is a minimized, versioned summary of an Atlas landscape
for **consented consumers**: partners, integrators and auditors who need to see *what* is
in a landscape — its systems, applications, platform substrate and vendors — without
receiving the customer-owned catalogue itself.

It is a **push-only sharing surface**: Atlas produces the digest and delivers it as a
file or over an outbound HTTPS call. A consumer never pulls from the customer's network.

Important boundary:

- the digest is a **summary for sharing** — not the customer-owned portability export
  ([`landscape.schema.json`](../schemas/v1/landscape.schema.json), see
  [`portability.md`](portability.md)) and not an import document;
- minimization is enforced by the schema (`additionalProperties: false`), not by
  convention: no hostnames, IP addresses, server or discovery details, descriptions,
  attachments, people, column-level data or credentials can appear in a digest;
- a digest carries **classification, names, lifecycle and counts**; v2 adds optional
  minimized topology and layer hints, described below.

## Document shape

```jsonc
{
  "contractVersion": "1",
  "digestId": "0195f2a4-7c3e-7000-8000-000000000043",
  "generatedAt": "2026-09-28T12:00:00Z",
  "sourceInstanceId": "atlas-instance-conformance",
  "sequence": 1,
  "scope": {
    "kinds": ["system", "application", "platform", "vendor"],
    "tags": [{ "key": "shared", "value": "true" }]
  },
  "items": [
    { "kind": "system", "name": "Payments platform", "lifecycle": "active", "integrationCount": 5 },
    { "kind": "application", "name": "Checkout service", "lifecycle": "active",
      "vendor": "in-house", "plannedChange": "replace", "integrationCount": 3 },
    { "kind": "platform", "name": "EU production compute", "lifecycle": "active", "vendor": "Example Cloud" },
    { "kind": "vendor", "name": "Example Corp", "lifecycle": "active" }
  ]
}
```

| Field | Meaning |
|---|---|
| `contractVersion` | The atlas-contracts schema major version (`"1"`). |
| `digestId` | Stable, opaque identifier for this digest (UUIDv7 recommended). |
| `generatedAt` | When the digest was produced (RFC 3339). |
| `sourceInstanceId` | Opaque identifier of the producing Atlas instance. Carries no tenant or customer semantics; it scopes sequence/replay state. |
| `sequence` | Monotonic per `sourceInstanceId` — see [Replay protection](#replay-protection). |
| `scope` | Which kinds and tag filters qualified inclusion — see [Scope](#scope). |
| `items` | The minimized items — see [Items and kinds](#items-and-kinds). |

## Items and kinds

A digest item carries only what a consented consumer needs: classification, name,
lifecycle, and optional vendor / planned change / integration count.

| Digest kind | Covers |
|---|---|
| `system` | catalogue `system` |
| `application` | catalogue `application` |
| `platform` | catalogue `server`, `infrastructure` and AI substrate (services, models) |
| `vendor` | a producer-derived summary entry (a vendor present in the landscape) |

The digest deliberately uses a **narrower vocabulary** than the catalogue `AssetKind`:
`server` and `infrastructure` fold into `platform`, and the data-layer kinds
(`data-area`, `dataset`, `column`) are excluded from digests entirely. A payload that
uses a catalogue kind where a digest kind is expected is rejected by the schema.

`plannedChange` uses the digest vocabulary `add` / `change` / `replace` / `retire` —
the Target Architecture contract's change intents extended with `replace`. The shared
`ChangeIntent` vocabulary is not modified.

`integrationCount` is a **count only** — never the identities of the integrated parties.

## Scope

`scope` records which asset kinds and tags qualified inclusion in this digest:

- `kinds` — the digest item kinds included. May be empty (no kinds included).
- `tags` — the tag filters that qualified inclusion. May be empty (no tag filter was
  applied). Tag matching is **OR** within the list and **AND** with the kind filter.

Both arrays are required. A digest-level invariant goes beyond what JSON Schema can
express: **every item's `kind` must be a member of `scope.kinds`**. The conformance kit
checks it; a consumer may rely on it.

## Replay protection

`sequence` is monotonic per `sourceInstanceId`:

- the first digest from a source is `sequence: 1`;
- each later digest is **strictly higher** (gaps are allowed);
- a consumer **rejects** any digest whose sequence is at or below the last one
  accepted for the same source — that is the replay protection;
- a source that resets its sequence must mint a **new `sourceInstanceId`**.

Sequence state is scoped per source: two different sources run independent monotonic
lines. The conformance kit ships a reference consumer-side tracker
(`SequenceTracker`) that implements exactly these rules.

## Detached signature

A produced digest may be delivered with a **detached signature** so a consumer can
verify integrity and origin without trusting the transport:

```jsonc
{
  "digest": { /* landscape-digest.schema.json */ },
  "signature": {
    "algorithm": "rsa-pkcs1-v1_5-sha256",
    "keyId": "atlas-conformance-test-key",
    "publicKey": "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A…",
    "value": "h5tqd4qNjFeMVG15wbFE9Ppi2mdSsTc4…"
  }
}
```

- The signature is computed over the **RFC 8785 (JCS) canonicalization** of the
  `digest` object: object keys sorted by UTF-16 code units, no whitespace, standard
  ECMAScript JSON string and number serialization (Unicode is preserved; strings
  are not normalized). Canonicalizing first means the signature does not depend on key order or
  formatting of the delivered JSON.
- `algorithm` is `rsa-pkcs1-v1_5-sha256` (SHA-256, PKCS#1 v1.5 — deterministic).
- `publicKey` is the verifier's SubjectPublicKeyInfo, base64url; `value` is the
  signature, base64url.
- `keyId` lets a consumer select the trust anchor. Key enrollment and trust-anchor
  distribution are out of scope for this repository.

The conformance fixtures use a **fixed, documented test key** (a test vector, not a
secret) so the signature bytes are stable and the fixtures can be regenerated and
verified from public feeds only.

## Conformance

- `schemas/v1/landscape-digest.schema.json` — the authoritative schema.
- `conformance/samples/landscape-digest.valid.json` — a valid digest.
- `conformance/samples/landscape-digest.forbidden-field.json` — a digest with
  forbidden fields (description, hostname); rejected by the schema.
- `conformance/samples/landscape-digest.signed.valid.json` /
  `landscape-digest.signed.tampered.json` — the signed wrapper with a verifying
  signature, and the same signature over a modified payload (fails verification).
- The .NET conformance kit (`conformance/dotnet/`) proves all of the above, plus the
  scope invariant and the replay rules.

## Versioning & compatibility

- Digest **v1 is frozen unchanged** under `schemas/v1/landscape-digest.schema.json`.
  Existing exporters may continue to emit v1. It has no refs, layers or relations.
- Digest **v2 coexists** under `schemas/v2/landscape-digest.schema.json`; SDKs expose
  `LandscapeDigestV2`, `LandscapeDigestScopeV2`, `LandscapeDigestItemV2` and
  `LandscapeDigestRelation`. Other document contracts remain v1. Package versions
  are tag-derived and independent of a digest's `contractVersion`.
- A v2 reader dispatches by `contractVersion`, validates with that version's schema
  and semantic rules, and accepts both v1 and v2. A v1 reader cannot accept v2.
  Do not strip v2 fields and reuse its signature as a v1 signature.
- There is no v1 removal or deprecation deadline. Producers migrate only after the
  receiving product supports v2 and the corresponding contract package is published.
  The decision and migration policy are recorded in
  [ADR 0001](decisions/0001-landscape-digest-v2.md).

## Digest v2: refs, layers and minimized relations

See the complete [stage 1 example](../conformance/samples/landscape-digest-v2.valid.json)
and [default-off example](../conformance/samples/landscape-digest-v2.without-relations.json).

Every v2 item requires `ref`: an opaque string of 1–64 characters, unique within
this digest and meaningless outside it. Mint new refs for each digest; never use
catalogue asset ids, stable hashes of ids, or persistent aliases. Validation can
check length and uniqueness, but cannot prove how a producer minted the ref.
The consuming product continues to identify items by `(kind, name)`; refs only
resolve edges inside the received document.

`layer` is an optional digest classification derived from the asset kind, not a
catalogue field or an ArchiMate requirement:

| Catalogue kind | Digest layer |
|---|---|
| system, application | application |
| server, infrastructure, AI service/model substrate | infrastructure |
| synthesized vendor summary | omit layer |

`business` and `data` are reserved enum values for forward compatibility. They
are not produced today because business processes and data-layer assets are
excluded from digests. The schema defines the vocabulary; it does not infer layers.

The closed relation vocabulary is:

| Kind | Endpoints and disclosure |
|---|---|
| `runsOn` | system/application → platform; reveals hosting association |
| `suppliedBy` | non-vendor component → vendor item; reveals supplier association |
| `integrates` | system/application ↔ system/application; reveals exchange topology, undirected, stage 2 only |

A vendor target is a synthesized summary of a vendor name found on an application
or AI service/model, not a catalogue vendor asset. It must appear as a `kind: vendor`
item in this digest, so `scope.kinds` must include `vendor`. Omit an edge when either
endpoint was filtered out; no dangling placeholder may be sent.

Edges have exactly `from`, `to` and `kind`. They never carry a label, direction
text, protocol, port, host, URL, data description, data count or other metadata.
`additionalProperties: false` applies to every edge, item, scope and root.

### Consent and stages

`scope.relations` is optional and defaults to **false** when absent. JSON Schema's
`default` is documentation, not a mutation of the incoming payload. `relations`
must be absent unless this flag is exactly `true`, even for an empty array.
The .NET SDK defaults the flag to false and omits null `Relations` when using
`AtlasContracts.SerializerOptions`. TypeScript callers omit the property.

Stage 1 supports `runsOn`, `suppliedBy` and `layer`. `integrates` is defined in the
closed contract vocabulary so a future stage 2 can use it, but the consuming
product rejects it until a separate explicit ADR addendum and consent permit it.
The stage 2 fixture proves vocabulary conformance, **not** permission to send it.

Relations are opt-in per customer, using the separate Fabric relations grant.
**Payload scope is never authorization**: a signed `scope.relations: true` records
sender intent, but cannot mint a grant, override revocation, or authorize stage 2.
Both sender and receiver must enforce trusted grants and their own stage policy
outside this contract. No exporter, storage, reader or drawing behavior is implemented here.

### Validation and signing

Before acceptance, validate the raw JSON with the correct schema **and** the
cross-item semantic rules. Do this before deserializing away unknown properties:

1. Schema: required refs, length 1–64, closed fields and enums, opt-in flag and
   at most **20,000** relations (inclusive). Exact duplicate edge objects fail too.
2. Semantics: unique refs, every item kind in `scope.kinds`, endpoints present in
   this digest, no self edge, correct endpoint kinds, and no duplicate edge by
   `(kind, from, to)`. For `integrates`, normalize endpoint order for duplicate
   detection, so A↔B and B↔A are duplicates. Directed relation kinds preserve order.
3. Trusted authorization and stage policy, signature/trust-anchor verification,
   and the unchanged per-source replay rules must also pass before consumption.

Both SDKs expose the semantic step: .NET
`LandscapeDigestV2Validation.Validate(digest)` and TypeScript
`validateLandscapeDigestV2(digest)`. An empty result means those semantic checks
passed; neither function replaces JSON Schema validation or authorizes sharing.
Neither can detect stable asset ids masquerading as opaque refs.

The existing detached signature wrapper and algorithm apply unchanged. JCS covers
the **entire** digest, including refs, layers, relations and `scope.relations`.
Sign the raw payload; never insert a default or reconstruct it with SDK defaults
before verification. Trust a previously enrolled key, not a wrapper's self-asserted
public key. The fixture key remains a documented public test vector.

The v2 fixtures under `conformance/samples/landscape-digest-v2.*.json` cover valid
stage 1, default-off and omitted flags, contract-only stage 2, forbidden edge
properties, unknown kinds, missing/invalid/duplicate refs, dangling/self/duplicate
edges (including reversed integration duplicates), wrong endpoint kinds, scope
mismatch, and signed edge tampering. Tests additionally cover 20,000/20,001 edges,
all v2 additions under the signature, Unicode, formatting and property order.
The .NET v2 JCS test helper supports safe integer counts/sequences in these vectors;
it is not a general-purpose ECMAScript number serializer. Use a complete RFC 8785
implementation for production verification.

Run `dotnet test AtlasContracts.slnx -c Release` for schema and .NET conformance,
and `npm test` in `sdk/typescript` for TypeScript semantic and signature conformance.
Both packages ship the v1 and v2 schemas together, including the relative v1 refs
used by the v2 schema for unchanged fields.

### Publication prerequisite

Implementation does not complete the publication acceptance in issue #45. Before
the Atlas exporter can depend on v2, a maintainer must authorize a tag-driven
release and publish the same version to **nuget.org and npm**, then verify both
public packages include the v2 SDK and schema. Downstream work pins that published
version. See [releasing.md](releasing.md); no release is performed by this change.
