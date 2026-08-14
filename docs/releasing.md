# Releasing & publishing

How a version of the Atlas contracts is cut and published. Two workflows do the
work, and both are deliberately separate so building a release and pushing it to
the public registries are distinct, gated steps.

| Workflow | Trigger | What it does |
|---|---|---|
| [`release.yml`](../.github/workflows/release.yml) | push a `v*.*.*` tag | Builds + tests from public feeds, packs both SDKs, attaches a **Sigstore build-provenance** attestation, and creates a **GitHub Release** with the versioned artifacts. Does not touch the registries. |
| [`publish.yml`](../.github/workflows/publish.yml) | manual (`workflow_dispatch`), gated by the `release` environment | Publishes `Vev.Atlas.Contracts` to **nuget.org** and `@vev-software/atlas-contracts` to **npm**. |

## Versioning

The package version follows SemVer and is independent of the schema
`contractVersion` (which is the `v1` contract major — see
[portability.md](portability.md) for the additive-vs-breaking policy). Breaking a
published contract requires an ADR, a migration path and a deprecation period; a
`v1` document always validates against the `v1` schemas.

## Cutting a release

1. Land the change on `main` (green CI: build, conformance, format, and the npm
   schema-packaging check).
2. Push a version tag, e.g. `git tag v0.1.0 && git push origin v0.1.0`. This runs
   `release.yml`: it produces the signed artifacts and the GitHub Release.
3. When you want the packages on the public registries, run `publish.yml`:
   **Actions → publish → Run workflow** (you can publish nuget and npm
   independently via the inputs). This step is manual on purpose.

Both SDKs ship the JSON Schemas: the NuGet package bundles `schemas/` directly,
and the npm package copies them in at pack time (`sdk/typescript/scripts/prepack.mjs`),
so a consumer of either package can validate payloads offline.

## Publishing without API keys — Trusted Publishing

Publishing uses **OIDC Trusted Publishing**, not stored API keys. nuget.org now
[discourages long-lived API keys for CI](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing):
instead the workflow presents a short-lived GitHub OIDC token, and the registry
mints a temporary credential because this repository and workflow are registered
as a trusted publisher. Nothing long-lived is stored in the repo, so there is no
key to leak or rotate.

- **NuGet** exchanges the OIDC token via `NuGet/login@v1` for a short-lived key.
- **npm** publishes tokenlessly over OIDC and attaches a provenance statement.

The one-time trusted-publisher setup lives with the maintainers (it is registry
account configuration, not repo configuration). Maintainers: see the internal
`engineering` runbook `runbooks/trusted-publishing.md`.

## Current status

Pre-release: the packages are not yet published to nuget.org / npm. The pipeline
above is in place and gated; the first publish is a deliberate maintainer action.
