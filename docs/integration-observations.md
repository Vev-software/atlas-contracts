# Integration and business-process observations

Discovery observations use `kind: "integration"` for a source-declared integration
and `kind: "business-process"` for a source-declared business process. Neither is
an application or a system. These kinds also work in catalogue exports and imports.

Use the source's stable identifier as `observedId`, namespaced by resource type when
needed. Carry durable source keys in `fingerprint`, source labels in `tags`, and
observation timestamps in `firstSeen`/`lastSeen`. These existing held-fact fields
are sufficient for the initial contract; no source-specific detail object is added.

Relationships use scanner-local IDs: an integration `connects-to` its observed
endpoints; a process `depends-on` the applications or integrations it uses; a
subprocess `part-of` its parent process. Emit these only when recorded by the source,
not inferred from a port scan. Endpoint references must be resolved by the consumer;
JSON Schema validates their shape, not their existence or semantic suitability.

Observations still cannot supply catalogue IDs, lifecycle, analysis, or detail
objects belonging to a different asset kind.

## Compatibility

Existing wire values and .NET enum numeric values are unchanged. Existing v1
documents remain valid. Older consumers with closed kind vocabularies cannot read
the two new values: upgrade the schema/SDK and consumer kind handling before enabling
these observations in a producer. Never silently coerce them to an existing kind.
The TypeScript union addition can require updates to exhaustive switches. No existing
kind is deprecated and no package version is hand-maintained; release remains tag-driven.
