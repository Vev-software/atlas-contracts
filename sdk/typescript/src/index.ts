/**
 * Public data model for VEV Atlas. Catalogue concepts only — no integration-criticality, EOL risk,
 * portfolio scoring or AI review (those work with the data and live in the private Atlas core).
 * See handbook 11 §1. Mirrors the authoritative JSON Schemas in `schemas/v1`.
 */

/** The atlas-contracts schema major version. */
export const CONTRACT_VERSION = "1" as const;

export type AssetKind =
  | "system"
  | "application"
  | "server"
  | "infrastructure"
  | "data-area"
  | "dataset"
  | "column";

export type Lifecycle = "draft" | "active" | "retired";

export type RelationshipType =
  | "runs-on"
  | "hosts"
  | "connects-to"
  | "depends-on"
  | "part-of"
  | "joins-on";

/** A manual, lightweight classification: a key with an optional value. */
export interface Tag {
  key: string;
  value?: string | null;
}

/** Held application metadata. Cataloguing only. */
export interface ApplicationDetails {
  version?: string | null;
  vendor?: string | null;
  businessOwner?: string | null;
}

/** Held server metadata. OS is a recorded fact, not an EOL/risk assessment (paid Atlas core). */
export interface ServerDetails {
  hostname?: string | null;
  environment?: string | null;
  operatingSystem?: string | null;
}

/** Held infrastructure metadata. Cataloguing only. */
export interface InfrastructureDetails {
  category?: string | null;
  location?: string | null;
}

/**
 * Held data-area (dataområde) metadata. Cataloguing only: `realisation` is free metadata
 * (e.g. "microservice", "reference-catalogue", "spreadsheet"), not a locked vocabulary and not analysis.
 */
export interface DataAreaDetails {
  realisation?: string | null;
}

/**
 * Held dataset (datamodel) metadata. Physical name and ownership are recorded facts, never quality
 * or classification verdicts (those are paid Atlas core).
 */
export interface DatasetDetails {
  physicalName?: string | null;
  owner?: string | null;
}

/**
 * Held column (kolonne) metadata. The declared data type and nullability are recorded facts.
 * NO analysis: no quality score, no classification verdict, no PII/sensitivity flag (paid Atlas core).
 */
export interface ColumnDetails {
  dataType?: string | null;
  nullable?: boolean | null;
}

/** A single catalogued asset. */
export interface Asset {
  id: string;
  kind: AssetKind;
  name: string;
  lifecycle: Lifecycle;
  description?: string | null;
  tags?: Tag[];
  application?: ApplicationDetails;
  server?: ServerDetails;
  infrastructure?: InfrastructureDetails;
  dataArea?: DataAreaDetails;
  dataset?: DatasetDetails;
  column?: ColumnDetails;
}

/** A manual, catalogue-level typed link between two assets. */
export interface Relationship {
  id: string;
  fromId: string;
  toId: string;
  type: RelationshipType;
  description?: string | null;
}

/** Provenance for an export: what produced the document. Held metadata, not analysis. */
export interface Generator {
  name: string;
  version?: string | null;
}

/**
 * The export side of the portability surface: a whole landscape carried across a boundary as a
 * self-consistent, resolved document. The matching import side is `ImportBundle`.
 */
export interface LandscapeDocument {
  contractVersion: typeof CONTRACT_VERSION;
  exportedAt?: string | null;
  generator?: Generator | null;
  assets: Asset[];
  relationships?: Relationship[];
}

/** How an import bundle applies to the existing catalogue. */
export type ImportMode = "merge" | "replace";

/**
 * An asset to import. Carries at least one of `id` (an existing Atlas id) or `externalId`
 * (a source-system id) so it can be matched and referenced.
 */
export interface ImportAsset {
  id?: string;
  externalId?: string;
  kind: AssetKind;
  name: string;
  lifecycle: Lifecycle;
  description?: string | null;
  tags?: Tag[];
  application?: ApplicationDetails;
  server?: ServerDetails;
  infrastructure?: InfrastructureDetails;
  dataArea?: DataAreaDetails;
  dataset?: DatasetDetails;
  column?: ColumnDetails;
}

/**
 * A manual relationship to import. Endpoints are references — an asset `id` or `externalId`
 * present in the bundle (or already in the catalogue).
 */
export interface ImportRelationship {
  id?: string;
  fromRef: string;
  toRef: string;
  type: RelationshipType;
  description?: string | null;
}

/**
 * The import side of the portability surface: a batch of catalogue assets and manual
 * relationships moved into Atlas.
 */
export interface ImportBundle {
  contractVersion: typeof CONTRACT_VERSION;
  kind: "import";
  mode?: ImportMode;
  assets: ImportAsset[];
  relationships?: ImportRelationship[];
}

// --- Discovery ingestion (the wire format a scanner emits into Atlas) ---
//
// Facts only: an Observation has no Atlas `id` and no `lifecycle` (the private Atlas
// Enterprise reconciliation engine assigns those), and never any analysis. This contract is
// open so third parties can build scanners; the reconciliation logic that consumes it is the
// private moat and is NOT part of this repo. See handbook 11 §3, 12 §Phase A.

/** How a discovery observation batch was collected. */
export type CollectionMethod = "agent" | "agentless";

/** Transport protocol of an observed port. */
export type PortProtocol = "tcp" | "udp";

/**
 * Which scanner produced a batch. The authoritative tenant binding is the authenticated
 * machine-principal at ingestion (a Fabric concern); `tenantRef` is an optional hint only.
 */
export interface ObservationSource {
  agentId: string;
  method: CollectionMethod;
  version?: string | null;
  tenantRef?: string | null;
}

/**
 * One durable correlation attribute: a key and its observed value (both required — unlike a
 * `Tag`, a fingerprint carries no valueless keys).
 */
export interface FingerprintAttribute {
  key: string;
  value: string;
}

/** An observed listening port. A recorded fact, not a measured integration. */
export interface ObservedPort {
  port: number;
  protocol?: PortProtocol | null;
  address?: string | null;
}

/** An observed running process or service. A recorded fact. */
export interface ObservedProcess {
  name: string;
  port?: number | null;
}

/** An observed installed runtime or package. A recorded fact, not an EOL/risk assessment. */
export interface ObservedPackage {
  name: string;
  version?: string | null;
}

/**
 * A single raw sighting in the landscape. Facts only: no Atlas `id` and no `lifecycle` (the
 * private reconciler assigns catalogue identity and state). Kind-specific held facts reuse the
 * catalogue vocabulary; ports/processes/packages are extra observations, not catalogue analysis.
 */
export interface Observation {
  observedId: string;
  kind: AssetKind;
  name: string;
  fingerprint?: FingerprintAttribute[];
  ports?: ObservedPort[];
  processes?: ObservedProcess[];
  packages?: ObservedPackage[];
  tags?: Tag[];
  firstSeen?: string | null;
  lastSeen?: string | null;
  application?: ApplicationDetails;
  server?: ServerDetails;
  infrastructure?: InfrastructureDetails;
}

/**
 * The public wire format a discovery scanner emits into Atlas: a batch of raw observations that
 * the private Atlas Enterprise reconciliation engine turns into catalogue assets.
 */
export interface DiscoveryObservationBatch {
  contractVersion: typeof CONTRACT_VERSION;
  kind: "discovery-observation";
  source: ObservationSource;
  observedAt: string;
  observations: Observation[];
}
