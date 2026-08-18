# @vev-software/atlas-contracts

Public, versioned **data model and interop contracts** for VEV Atlas — the portability and
interoperability surface that the Atlas runtime and third-party importers/exporters all build
against, without reaching into anyone's internals.

```sh
npm i @vev-software/atlas-contracts
```

```ts
import type { LandscapeDocument, Asset } from "@vev-software/atlas-contracts";
```

## What's inside

- **Asset data model** — the catalogue of systems, applications, servers and infrastructure,
  plus the manual **relationships** and **tags** that connect and classify them.
- **Landscape import/export document** — the schema that carries a whole landscape across a
  boundary (customer-owned export; community importers/exporters).
- **Authoritative JSON Schemas** — shipped under `schemas/` in the package, so you can validate
  payloads against the exact same contracts the TypeScript types describe.

Ships type declarations (`dist/index.d.ts`) and ESM; no runtime dependencies.

## Versioning

[SemVer](https://semver.org), derived from the git tag (`vX.Y.Z`) at release. Pre-1.0, the v1
contract evolves additively across minor/patch versions.

## Links

- **Source & issues:** https://github.com/Vev-software/atlas-contracts
- **.NET SDK (NuGet):** [`Vev.Atlas.Contracts`](https://www.nuget.org/packages/Vev.Atlas.Contracts)

## Licence

[Apache-2.0](https://github.com/Vev-software/atlas-contracts/blob/main/LICENSE).
