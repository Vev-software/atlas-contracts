# Portability surface — import & export

The portability surface is the public, machine-checkable contract for moving a whole
landscape across a boundary: **customer-owned data export** (you can always take your
catalogue with you) and **third-party interop** (importers/exporters validate their
payloads against one published schema). It carries **catalogue data only** — assets,
manual relationships and tags — never paid-core analysis outputs (integration
criticality, EOL, portfolio scoring, AI review), which live in the private Atlas core
(handbook `11 §1-3`, `12 §Phase C`).

Two documents, one shared vocabulary (`common.schema.json`, `asset.schema.json`,
`relationship.schema.json`):

| Direction | Schema | SDK type | Purpose |
|---|---|---|---|
| Export (out of Atlas) | [`landscape.schema.json`](../schemas/v1/landscape.schema.json) | `LandscapeDocument` / `LandscapeDocument` (TS) | The portability promise: a self-consistent, fully-resolved snapshot. |
| Import (into Atlas) | [`import.schema.json`](../schemas/v1/import.schema.json) | `ImportBundle` / `ImportBundle` (TS) | A batch to apply, with reference resolution and a merge/replace mode. |

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
