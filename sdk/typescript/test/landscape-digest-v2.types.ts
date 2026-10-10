import type { LandscapeDigest, LandscapeDigestV2, LandscapeDigestItemV2, LandscapeDigestRelation } from "../dist/index.js";

const v1: LandscapeDigest = {
  contractVersion: "1", digestId: "example-v1", generatedAt: "2026-10-09T12:00:00Z",
  sourceInstanceId: "example-source", sequence: 1, scope: { kinds: ["system"], tags: [] },
  items: [{ kind: "system", name: "Example ERP", lifecycle: "active" }]
};
const v2: LandscapeDigestV2 = {
  ...v1, contractVersion: "2", scope: { ...v1.scope, relations: true },
  items: [
    { ref: "c1", kind: "system", name: "Example ERP", lifecycle: "active", layer: "application" },
    { ref: "c2", kind: "platform", name: "Example compute", lifecycle: "active", layer: "infrastructure" }
  ], relations: [{ from: "c1", to: "c2", kind: "runsOn" }]
};
// @ts-expect-error v2 requires a per-digest ref.
const missingRef: LandscapeDigestItemV2 = { kind: "system", name: "Example", lifecycle: "active" };
// @ts-expect-error only from, to and kind belong to an edge.
const extraEdge: LandscapeDigestRelation = { from: "c1", to: "c2", kind: "runsOn", protocol: "forbidden" };
// @ts-expect-error relation kinds are closed.
const unknownKind: LandscapeDigestRelation = { from: "c1", to: "c2", kind: "dependsOn" };
// @ts-expect-error v2 cannot be assigned to v1.
const wrongVersion: LandscapeDigest = v2;
void [v1, v2, missingRef, extraEdge, unknownKind, wrongVersion];
