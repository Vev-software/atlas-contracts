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
- a digest carries **classification, names, lifecycle and counts** — never the
  customer-owned detail behind them.

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
  JSON string escaping (characters above U+2027 escaped), whole numbers in integer
  form. Canonicalizing first means the signature does not depend on key order or
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

- The surface is **v1**: `contractVersion` is `"1"` and the schema lives under
  `schemas/v1/`.
- **Additive changes are non-breaking** and ship within v1: new optional fields and
  new enum members (a new digest kind or planned change).
- **Breaking changes** — removing/renaming a field, tightening a constraint, changing
  the meaning of an existing value — require a new major (`schemas/v2/`) plus an ADR,
  a migration path and a deprecation period. A v1 digest will always validate
  against the v1 schema.
