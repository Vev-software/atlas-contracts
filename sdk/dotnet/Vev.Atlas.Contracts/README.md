# Vev.Atlas.Contracts

Public, versioned **data model and interop contracts** for VEV Atlas — the portability and
interoperability surface that the Atlas runtime and third-party importers/exporters all build
against, without reaching into anyone's internals.

```sh
dotnet add package Vev.Atlas.Contracts
```

## What's inside

- **Asset data model** — the catalogue of systems, applications, servers and infrastructure,
  plus the manual **relationships** and **tags** that connect and classify them.
- **Landscape import/export document** — the schema that carries a whole landscape across a
  boundary (customer-owned export; community importers/exporters).
- **Authoritative JSON Schemas** — bundled under `schemas/` in the package, so any language can
  validate against the exact same contracts the SDK exposes.

The types are plain records/enums with `System.Text.Json` attributes and no runtime dependencies
beyond the base class library.

## Versioning

[SemVer](https://semver.org), derived from the git tag (`vX.Y.Z`) at release. Pre-1.0, the v1
contract evolves additively across minor/patch versions.

## Links

- **Source & issues:** https://github.com/Vev-software/atlas-contracts
- **TypeScript SDK (npm):** [`@vev-software/atlas-contracts`](https://www.npmjs.com/package/@vev-software/atlas-contracts)

## Licence

[Apache-2.0](https://github.com/Vev-software/atlas-contracts/blob/main/LICENSE).
