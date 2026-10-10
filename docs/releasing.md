# Releasing & publishing

How a version of the Atlas contracts is cut and published. The manual release
workflow verifies main, computes a tag-derived version and requires the release
environment's approval before tagging and publishing.

| Workflow | Trigger | What it does |
|---|---|---|
| [`publish.yml`](../.github/workflows/publish.yml), displayed as `release` | manual (`workflow_dispatch`) on main with `bump` and `dry_run` | Requires green CI for main's exact commit, computes the next version, then after the `release` environment approval builds, tests, tags and publishes both SDKs to **nuget.org** and **npm**. `dry_run` skips tagging and publication. |
| [`release.yml`](../.github/workflows/release.yml), displayed as `github release` | push a `v*.*.*` tag | Builds + tests from public feeds, packs both SDKs, attaches a **Sigstore build-provenance** attestation and creates a **GitHub Release**. Does not touch the registries. |

## Versioning — the tag is the single source of truth

There is **no hand-maintained version number** in this repo. The git tag drives
everything:

- **.NET** — [MinVer](https://github.com/adamralph/minver) derives the package
  version from the tag at build time (`MinVerTagPrefix` is `v`, so tag `vX.Y.Z` →
  package `X.Y.Z`; commits after a tag produce a pre-release). Configured in
  [`Directory.Build.props`](../Directory.Build.props).
- **npm** — the workflows stamp `package.json` from the tag with
  `npm version ${TAG#v}` at pack/publish time. The committed `version` is
  `0.0.0` (a placeholder); CI overwrites it.

So bumping a release is a **single action: create the tag**. Nothing else to edit,
so the .NET and npm packages can never drift out of sync. Version follows SemVer and
is independent of the schema `contractVersion` (including digest v1/v2 — see
[portability.md](portability.md) for the additive-vs-breaking policy). Breaking a
published contract still requires an ADR, a migration path and a deprecation period.

> MinVer needs the full git history + tags, so every job that builds/packs checks
> out with `fetch-depth: 0`.

## Cutting a release

1. Land the change on `main` (green CI: build, conformance, format, and the npm
   schema-packaging check).
2. After explicit maintainer authorization, open **Actions → release → Run
   workflow** on main, choose `bump` (patch/minor/major), and leave `dry_run` false
   for publication. The workflow computes the next tag; no package version is edited.
3. Approve the `release` environment gate. The workflow tests the verified commit,
   creates/pushes the tag and publishes both public packages; the tag also triggers
   `github release` for release artifacts and provenance.
4. Verify that the same version is available from both public feeds and that its
   schemas and SDK surfaces are present. Exporters must pin this published version
   before depending on new contract fields. Digest v2 publication remains outstanding
   until these checks pass; implementation and local packing do not satisfy it.

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

Published: `Vev.Atlas.Contracts` is on **nuget.org** and
`@vev-software/atlas-contracts` is on **npm** (first release `0.1.0`). The pipeline
above is in place and gated; each subsequent publish is a deliberate maintainer
action against a released tag.
