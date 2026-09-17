# ADR 0001: Preserve source integration and process kinds

Status: Proposed for review with issue #38.

## Context

Source inventories contain integrations and business processes. Mapping these
to applications or systems changes their meaning. The kind vocabulary is shared
by discovery, catalogue export and import, and some consumers use exhaustive enums.

## Decision

Append `integration` and `business-process` to the existing v1 vocabulary, with
matching .NET and TypeScript values. Reuse source IDs, fingerprints and tags for
held facts and the existing observed relationship vocabulary. Do not introduce
analysis fields or vendor-specific detail objects.

## Consequences and migration

Previously valid payloads and numeric .NET enum values remain unchanged. Readers
must be upgraded before producers emit the new kinds; exhaustive switches need
explicit cases. Old readers must not be sent the new kinds or receive coerced
application/system substitutes. No field or old kind is removed or deprecated.
Consumer rollout is a prerequisite to production emission and is outside this SDK
change. Conformance covers discovery round trips, catalogue/import compatibility,
and rejection of catalogue state and mismatched details in observations.

## Alternatives

Overloading application/system loses meaning. Tags alone do not provide a stable
kind discriminator. A new schema major for otherwise unchanged documents would
force unrelated producers to migrate; retain v1 with explicit consumer sequencing.
