# Portability surface — import, export and hosted migration

The portability surface is the public, machine-checkable contract for moving Atlas data
across a boundary: **customer-owned data export** (you can always take your landscape with
you), **third-party interop** (importers/exporters validate payloads against one published
schema), and the foundation for **hosted ↔ self-hosted migration**. The current v1
contracts carry the public landscape core first — assets, manual relationships, tags and
the data-layer model — while leaving room for a richer bundle that layers hosted migration
metadata on top without introducing a private escape-hatch format.

Important boundary:

- public portability carries **Atlas domain data and migration metadata**;
- it does **not** carry entitlement state, subscription state, billing state, live identity credentials or raw secrets;
- paid analysis outputs may be included only where they are represented as Atlas-owned domain artefacts with a stable public contract. Pricing state itself is never the file format.

Two documents, one shared vocabulary (`common.schema.json`, `asset.schema.json`,
`relationship.schema.json`):

| Direction | Schema | SDK type | Purpose |
|---|---|---|---|
| Export (out of Atlas) | [`landscape.schema.json`](../schemas/v1/landscape.schema.json) | `LandscapeDocument` / `LandscapeDocument` (TS) | The portability promise: a self-consistent, fully-resolved snapshot. |
| Import (into Atlas) | [`import.schema.json`](../schemas/v1/import.schema.json) | `ImportBundle` / `ImportBundle` (TS) | A batch to apply, with reference resolution and a merge/replace mode. |
| Bundle (hosted ↔ self-hosted migration) | [`bundle.schema.json`](../schemas/v1/bundle.schema.json) | `AtlasBundle` / `AtlasBundle` (TS) | The landscape export plus migration metadata (workspace, diagrams, attachment manifest, module data, restore hints), as one self-contained package. |

## Portable bundle — the hosted migration package

The landscape export is enough to carry the catalogue across a boundary. A hosted ↔
self-hosted move needs a little more: the workspace's own metadata, its diagrams, the
attachments hanging off assets, any public module data, and hints for rebinding identity
and secrets at the destination. The **portable bundle** ([`bundle.schema.json`](../schemas/v1/bundle.schema.json),
`AtlasBundle`) packages all of that as one self-contained document, layered on top of the
landscape export — a **public** contract, not a private hosted-only format.

```jsonc
{
  "contractVersion": "1",
  "kind": "atlas-bundle",
  "createdAt": "2026-08-13T10:00:00Z",
  "generator": { "name": "Atlas Community", "version": "0.1.0" },
  "compatibility": {
    "minReaderContractVersion": "1",
    "producerAtlasVersion": "2026.8.0",
    "requiredModules": [{ "id": "atlas.diagramming", "minVersion": "1" }]
  },
  "manifest": {
    "landscape": { "assetCount": 4, "relationshipCount": 3 },
    "diagramCount": 1, "attachmentCount": 1, "moduleCount": 1
  },
  "landscape": { /* landscape.schema.json — the resolved core */ },
  "workspace": { "name": "Payments", "slug": "payments", "locale": "da-DK" },
  "diagrams":  [ /* held layouts referencing landscape ids */ ],
  "attachments": [ /* manifest: references + sha-256 digests, never inline bytes */ ],
  "modules":   [ /* public module/extension data, keyed by module id + its own version */ ],
  "restore":   { /* identity-mapping placeholders + secret-rebind instructions */ },
  "excluded":  ["secrets", "live-credentials", "entitlements", "billing"]
}
```

The bundle carries **Atlas domain data and migration metadata only**. What it does *not*
carry is fixed by the schema, not by convention:

| Section | Carries | Never |
|---|---|---|
| `landscape` | the resolved assets + relationships (the export contract) | paid-core analysis |
| `workspace` | display name, slug, locale, time zone, tags | tenant shell, subscription, entitlement, trial state |
| `diagrams` | held node/edge layouts referencing landscape ids; a `renderRef` to an attachment | inline rendered bytes |
| `attachments` | a **manifest** — id, name, media type, byte size, **sha-256 digest**, `attachedToRef` | the bytes themselves (they travel out of band) |
| `modules` | a public module id + the version of *its own* public contract + an opaque payload | secrets or entitlement state smuggled through a module |
| `restore` | identity-mapping placeholders (`sourceRef` + hint) and secret-rebind **instructions** (`ref` + how-to) | passwords, sessions, tokens or secret **values** |
| `excluded` | a machine-visible list of the categories deliberately left out | — |

Two bundle-level rules go beyond what JSON Schema can express; the SDK provides both, and
the conformance kit enforces them:

```csharp
var bundle = JsonSerializer.Deserialize<AtlasBundle>(json, AtlasContracts.SerializerOptions);
var lied   = bundle.ManifestErrors();        // empty ⇒ the manifest counts match the sections
var broken = bundle.UnresolvedReferences();  // empty ⇒ every diagram/attachment ref resolves in-bundle
```

`ManifestErrors()` catches a manifest that under- or over-counts a section (a truncated or
tampered bundle). `UnresolvedReferences()` catches a diagram node pointing at an absent
asset, an edge at an absent relationship, or a `renderRef`/`attachedToRef` pointing at
nothing — the self-consistency a destination relies on before it imports.

### Attachments and secrets stay out of the file

The attachment section is deliberately a manifest, not a blob store: it lists each
attachment by reference and **sha-256 digest** so the destination can fetch the bytes from
the transport channel and verify them, without the bundle ever becoming a place to hide
secret material. Likewise `restore` names *what* to rebind (an identity to map, a secret to
re-provide) and never the value. Raw secrets, live credentials and other operational state
are excluded structurally — every object is `additionalProperties: false`, so a stray
`bytes` or `password` field is rejected, not ignored.

## Export — the landscape document

A resolved snapshot: every asset already has its stable Atlas `id`, and relationships
point at those ids. Optional `exportedAt` and `generator` (`{ name, version }`) record
provenance — what produced the export and when.

```jsonc
{
  "contractVersion": "1",
  "exportedAt": "2026-08-06T09:00:00Z",
  "generator": { "name": "Atlas Community", "version": "0.1.0" },
  "assets": [ /* asset.schema.json */ ],
  "relationships": [ /* relationship.schema.json */ ]
}
```

An export is self-contained: it validates against `landscape.schema.json` and needs no
external context to be understood.

## Import — the bundle

An import bundle is a batch moved *into* Atlas. It differs from an export in two ways
that matter:

1. **Identity is not yet Atlas's.** An imported asset may not have an Atlas `id` — the
   runtime assigns one. Instead it carries an **`externalId`**: an opaque
   identifier from the source system (e.g. a CMDB record id). At least one of `id` or
   `externalId` is required, so every asset can be matched on re-import and referenced
   by relationships.
2. **Relationships are by reference.** Endpoints are **`fromRef` / `toRef`**, each
   matching an asset's `id` *or* `externalId` — in the same bundle, or already in the
   target catalogue.

`mode` records intent: `merge` (default) upserts by identifier; `replace` makes the
target match the bundle. The runtime enforces the semantics; the contract only records
the intent.

```jsonc
{
  "contractVersion": "1",
  "kind": "import",
  "mode": "merge",
  "assets": [
    { "externalId": "cmdb:APP-1043", "kind": "application", "name": "Checkout", "lifecycle": "active" }
  ],
  "relationships": [
    { "fromRef": "cmdb:APP-1043", "toRef": "srv-checkout-01", "type": "runs-on" }
  ]
}
```

### Reference resolution

JSON Schema fixes the *shape* of references but cannot express "every `fromRef`/`toRef`
resolves to an asset in the bundle" — that is a bundle-level rule. The SDK provides it:

```csharp
var bundle = JsonSerializer.Deserialize<ImportBundle>(json, AtlasContracts.SerializerOptions);
var dangling = bundle.UnresolvedReferences(); // empty ⇒ internally self-consistent
```

`UnresolvedReferences()` returns endpoints that match no asset **declared in the bundle**.
A non-empty result is only an error for a *self-contained* bundle; an endpoint may also
resolve against an asset already in the catalogue, which the bundle alone cannot see.

## Atlas-owned versus non-portable data

The portability promise applies to Atlas-owned domain data. It deliberately excludes or
special-cases the following:

| Category | Portability rule |
|---|---|
| Tenant shell, subscription, entitlements, trial/lifecycle state | Fabric/control-plane concern; recreated or remapped at the destination |
| Users and identity credentials | export references/mapping hints only; bind to the destination IdP separately |
| Secrets and integration credentials | never export raw secret values; export references or rebind requirements only |
| Billing/accounting records | not part of Atlas domain portability |
| Audit records and telemetry aggregates | separate operational/legal retention concern, not workspace payload |

This keeps Atlas portable without smuggling policy, secret or billing concerns into the
domain bundle.

## Data layer — down to the column

The catalogue models the data architecture *beneath* a system, so an export can carry it
across a boundary with no loss. Three additional asset kinds extend the same asset model
(reusing its authoring, tags, relationships and portability plumbing — no parallel store):

| Kind | Danish | Held metadata (`*Details`) | Cataloguing only — never |
|---|---|---|---|
| `data-area` | dataområde | `dataArea.realisation` | — |
| `dataset` | datamodel | `dataset.physicalName`, `dataset.owner` | quality/classification verdicts |
| `column` | kolonne | `column.dataType`, `column.nullable` | quality score, classification, PII/sensitivity flag |

Held facts only: a column carries its **name + declared type**, never an analysis verdict
(that works with the data and is paid Atlas core, `11 §1`). The schema enforces this —
`ColumnDetails` is `additionalProperties: false`, so a stray `classification` field is
rejected.

**Containment** is expressed with the existing `part-of` relationship, forming one chain:

```
column  ──part-of──▶  dataset  ──part-of──▶  data-area  ──part-of──▶  system
```

**Keys** (nøgler) are cross-dataset joins, modelled as a first-class `joins-on`
relationship between columns (or datasets) — a held link, not a measured/derived join.

### Containment validation

JSON Schema fixes each entity's shape but cannot express "every higher level points down
to a concrete dataset/column". The SDK provides that bundle-level invariant:

```csharp
var errors = landscape.DataLayerContainmentErrors(); // empty ⇒ every column→dataset→data-area→system resolves
```

Each `column` must be `part-of` exactly one `dataset`, each `dataset` exactly one
`data-area`, and each `data-area` exactly one `system`; anything else (a loose column, an
ambiguous parent, a skipped level) is reported. This is the invariant the catalogue's
"navigate all the way to the column" experience relies on.

## Validating a document

Everything builds from public feeds only. To self-certify a payload, run it against the
published schemas — the same conformance discipline the Atlas runtime uses for its own
exports (see `conformance/`).

- **.NET / any language:** validate the JSON against `schemas/v1/*.json` with any
  2020-12 JSON Schema validator; the schemas resolve each other by `$id`.
- **Round-trip:** the conformance kit proves an SDK-produced export both conforms and
  survives a deserialise → re-serialise round-trip unchanged — the check the runtime
  reuses to guarantee its exports stay valid.

## Versioning & compatibility

- The surface is **v1**: `contractVersion` is `"1"` and schemas live under `schemas/v1/`.
- **Additive changes are non-breaking** and ship within v1: new optional fields (e.g.
  `generator` was added this way) and new enum members (e.g. a new relationship type).
- **Breaking changes** — removing/renaming a field, tightening a constraint, changing the
  meaning of an existing value — require a new major (`schemas/v2/`) plus an **ADR, a
  migration path and a deprecation period** (`AGENTS.md §4`, handbook `03 · E3`). A v1
  document will always validate against the v1 schemas.
- The **bundle** follows the same discipline and adds a forward-compatibility handshake:
  `compatibility.minReaderContractVersion` states the lowest contract major a reader must
  implement to consume the bundle safely. A reader on an older major must **refuse** the
  bundle rather than silently drop the sections it cannot understand. Because new sections
  and fields are added *additively* within a major, a newer producer stays readable by an
  older-but-same-major reader, which imports the landscape core and skips what it does not
  know — the module compatibility hints (`compatibility.requiredModules`) tell it what it is
  missing.
