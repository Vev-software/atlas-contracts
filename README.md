# atlas-contracts

> **Visibility:** PUBLIC · **Licence:** Apache-2.0
> **Product:** Atlas · **Owner team:** atlas-maintainers

The public, versioned **data model and interop contracts** for Atlas — the schemas
and dual (.NET + TypeScript) SDK that the Atlas runtime, the Fabric platform and
third-party importers/exporters all build against, without reaching into anyone's
internals. This is the portability and interoperability surface for the Atlas
ecosystem.

## Status

**Experimental — scaffolding in progress.** There are no published schema or SDK
artifacts yet; the contract surface is still being stood up. Follow the epic and
the open work in
[Issues](https://github.com/Vev-software/atlas-contracts/issues).

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

Not available yet — the schema build and SDK packaging are being set up
([#2](https://github.com/Vev-software/atlas-contracts/issues/2)). This section will
carry the clone / build / publish steps once the pipeline lands.

## Roadmap

- [#1](https://github.com/Vev-software/atlas-contracts/issues/1) — [Epic] public data model & interop contracts
- [#2](https://github.com/Vev-software/atlas-contracts/issues/2) — Repo scaffolding: schema build + .NET/TS SDK packaging + conformance tests + CI
- [#3](https://github.com/Vev-software/atlas-contracts/issues/3) — Asset data model: systems / applications / servers / infrastructure (+ relationships & tags)
- [#4](https://github.com/Vev-software/atlas-contracts/issues/4) — Import/export schemas (portability surface)

## Contributing

One logical change per PR. See the Vev-software engineering handbook's contributing
guidelines; a DCO/CLA applies per this repo's Apache-2.0 licence.

## Security

Please report vulnerabilities **privately**, not through public issues — see the
Vev-software security policy.
