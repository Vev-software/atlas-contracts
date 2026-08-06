# atlas-contracts

> **Visibility:** PUBLIC · **Licence:** Apache-2.0
> **Product:** Atlas · **Owner team:** atlas-maintainers

The public, versioned **data model and interop contracts** for Atlas — the schemas
and dual (.NET + TypeScript) SDK that the Atlas runtime, the Fabric platform and
third-party importers/exporters all build against, without reaching into anyone's
internals. This is the portability and interoperability surface for the Atlas
ecosystem.

## Status

**Pre-release (v1 contract, unpublished).** The v1 asset data model and the
publishing pipeline are in place — JSON Schemas, the .NET and TypeScript SDKs, and
a runnable conformance kit, all built from public feeds only. Packages are not yet
published to nuget.org / npm. Follow the epic and the open work in
[Issues](https://github.com/Vev-software/atlas-contracts/issues)
([#2](https://github.com/Vev-software/atlas-contracts/issues/2) scaffolding,
[#3](https://github.com/Vev-software/atlas-contracts/issues/3) asset data model).

## What this is (and is not)

- **Is:** the public contract surface for Atlas —
  - the **asset data model** (systems, applications, servers, infrastructure, plus
    the manual relationships and tags that connect and classify them),
  - the **import/export document schemas** that carry a whole landscape across a
    boundary (customer-owned data export; community importers/exporters such as
    ArchiMate/BPMN),
  - **conformance tests** third parties can run, and
  - the **.NET and TypeScript SDKs** generated from the schemas.
- **Is not:** the Atlas application. The runtime lives in separate repositories
  (the free community edition and the commercial core) and is **not** part of this
  repo. This repo carries **no** commercial or analysis logic — only the portable,
  machine-checkable contracts.
- **Boundary:** Atlas domain concepts (asset / application / server /
  infrastructure) live here, never in `fabric` — the Fabric platform must not know
  what an "application portfolio" is. The public portability surface lives here,
  never inside the licensed Atlas runtime.

## Dependencies

Apache-2.0 and fully public: this repo builds with **nothing private**. It depends
only on published contracts (Fabric packages / product contracts), never on another
product's internals.

## Quickstart

Everything builds from public feeds only — no private repo or feed is required
(AGENTS.md §1.9). The .NET SDK is pinned via `global.json`.

```bash
# .NET SDK + conformance kit
dotnet build AtlasContracts.slnx -c Release
dotnet test  AtlasContracts.slnx -c Release   # runs the conformance tests
dotnet pack  sdk/dotnet/Vev.Atlas.Contracts/Vev.Atlas.Contracts.csproj -c Release

# TypeScript SDK
cd sdk/typescript && npm install && npm run build
```

Layout:

- `schemas/v1/` — the authoritative JSON Schemas (the source of truth).
- `sdk/dotnet/` — the `Vev.Atlas.Contracts` NuGet package (ships the schemas).
- `sdk/typescript/` — the `@vev-software/atlas-contracts` npm package.
- `conformance/` — a runnable kit a third party or the Atlas runtime uses to prove
  a payload matches the published schemas.

Third parties consume the published packages and run the conformance kit against
their own import/export payloads; the Atlas runtime pins a released contract version
and validates its exports against the same schemas.

## Contributing

One logical change per PR. See the Vev-software engineering handbook's contributing
guidelines; a DCO/CLA applies per this repo's Apache-2.0 licence.

## Security

Please report vulnerabilities **privately**, not through public issues — see the
Vev-software security policy.
