# Target Architecture Contract

The Target Architecture contract is the public, versioned **To-Be** surface for Atlas.
It carries the named target, its ordered versions, the status vocabulary and optional
transition/gap/work-package shapes. It is product-native truth only: ArchiMate remains
a peripheral mapping, never the internal model.

## Document shape

Top-level document: `TargetArchitectureDocument`

- `contractVersion` = atlas-contracts major version (`"1"` in v1)
- `kind` = `"target-architecture"`
- `id`, `name`, `description?`
- `scope` = `estate | domain | capability-area` plus optional `ref` / `name`
- `generator?` = producer provenance
- `versions[]` = ordered target versions
- `transitions[]` = optional intermediate transition/gap/work-package shapes

## Versions

Each `TargetVersion` carries:

- `id`, `name`, `sequence`
- `status` = `draft | proposed | approved | active | superseded | rejected | archived`
- `rationale?`
- `intendedHorizon?`
- `effectiveDate?`
- `assetChanges[]`
- `relationshipChanges[]`

The schema enforces one core invariant directly: a Target Architecture must carry
**exactly one** `active` version.

## Change intent

Target members use the public `ChangeIntent` vocabulary:

- `add`
- `change`
- `retire`

Asset and relationship changes can either:

- reference an existing catalogue item (`assetRef` / `relationshipRef`), or
- carry the intended target-state payload (`asset` / `relationship`)

Expected use:

- `add` => payload required
- `change` => reference + payload required
- `retire` => reference required

## Transitions and gaps

`transitions[]` is optional. It represents the intermediate path between two target
versions and may contain:

- `gaps[]` = optional intermediate delta slices
- `workPackages[]` = grouped work with `deliverables[]`

This is the shared public shape both editions can read. Rich gap analysis, visual
workbench behaviour and approval workflow stay in the product runtimes, not here.
