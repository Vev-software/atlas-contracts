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
  numericId?: number;
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
  dataArea?: DataAreaDetails;
  dataset?: DatasetDetails;
  column?: ColumnDetails;
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

// --- Portable Atlas bundle (the hosted <-> self-hosted migration package) ---
//
// A self-contained package layered ON TOP of the landscape export: the resolved landscape plus the
// Atlas-domain metadata a move needs (workspace metadata, diagrams, an attachment manifest, public
// module data, restore hints). It carries Atlas domain data and migration metadata only — never
// entitlement/subscription/billing state, live identity credentials or raw secret values. See
// handbook 11 §3, 11 §7, 12 §Phase C, ADR 0003. Mirrors `schemas/v1/bundle.schema.json`.

/** The layout format of a `Diagram`. */
export type DiagramFormat = "atlas-layout";

/** The digest algorithm of a `Digest`. */
export type DigestAlgorithm = "sha-256";

/** A category of state deliberately NOT portable through the bundle. */
export type ExcludedCategory =
  | "secrets"
  | "live-credentials"
  | "entitlements"
  | "subscription"
  | "billing"
  | "audit-logs"
  | "telemetry";

/** Counts for the embedded landscape core. */
export interface LandscapeCounts {
  assetCount: number;
  relationshipCount: number;
}

/** Machine-checkable inventory of what an `AtlasBundle` carries. Counts must match section sizes. */
export interface BundleManifest {
  landscape: LandscapeCounts;
  diagramCount?: number;
  attachmentCount?: number;
  moduleCount?: number;
}

/** A public module the destination should have to consume a module data section. */
export interface ModuleRequirement {
  id: string;
  minVersion?: string | null;
}

/** Producer-declared compatibility floor and provenance. */
export interface BundleCompatibility {
  /** Minimum atlas-contracts major a reader must implement to safely import. */
  minReaderContractVersion: typeof CONTRACT_VERSION;
  producerAtlasVersion?: string | null;
  requiredModules?: ModuleRequirement[];
}

/**
 * Portable workspace display metadata. Presentation only: never tenant shell, subscription,
 * entitlement, trial or billing state (recreated or remapped at the destination).
 */
export interface WorkspaceMetadata {
  name: string;
  slug?: string | null;
  description?: string | null;
  locale?: string | null;
  timeZone?: string | null;
  tags?: Tag[];
}

/** A placed asset in a diagram. Position is a held layout fact, not analysis. */
export interface DiagramNode {
  ref: string;
  x: number;
  y: number;
  label?: string | null;
}

/** A drawn relationship in a diagram. */
export interface DiagramEdge {
  ref: string;
}

/**
 * A portable diagram: a held layout over the landscape. Node placements reference asset ids and
 * edges reference relationship ids present in the embedded landscape; a rendered image, if any, is
 * carried as an attachment via `renderRef`, not inline.
 */
export interface Diagram {
  id: string;
  name: string;
  format: DiagramFormat;
  description?: string | null;
  nodes?: DiagramNode[];
  edges?: DiagramEdge[];
  renderRef?: string | null;
}

/** A content digest so the destination can verify out-of-band attachment bytes. */
export interface Digest {
  algorithm: DigestAlgorithm;
  value: string;
}

/**
 * One attachment listed by reference and integrity digest — never inline secret bytes. The bytes
 * travel out of band; this entry lets the destination locate and verify them.
 */
export interface AttachmentManifestEntry {
  id: string;
  name: string;
  mediaType: string;
  byteSize: number;
  digest: Digest;
  attachedToRef?: string | null;
}

/**
 * A public module / extension data section. The bundle contract fixes only the envelope (which
 * module, which of its contract versions); `data` is governed by that module's own public contract
 * and is opaque here. The same exclusions apply — no secrets or entitlement state.
 */
export interface ModuleData {
  id: string;
  version: string;
  itemCount?: number | null;
  data?: Record<string, unknown>;
}

/** One identity-mapping placeholder. Carries references and hints, never credentials. */
export interface IdentityMapping {
  sourceRef: string;
  displayName?: string | null;
  bindHint?: string | null;
}

/** One secret-rebind instruction. Names what must be re-provided, never the value. */
export interface SecretRebind {
  ref: string;
  instruction?: string | null;
}

/**
 * Non-data instructions for reconstructing the workspace at the destination. Placeholders and
 * instructions only: never password/session material or secret values.
 */
export interface RestoreHints {
  identityMappings?: IdentityMapping[];
  secretRebinds?: SecretRebind[];
  notes?: string | null;
}

/**
 * The public, versioned portable Atlas bundle: a self-contained migration package layered on top of
 * the landscape export (`LandscapeDocument`). Carries the resolved landscape plus the Atlas-domain
 * metadata a hosted <-> self-hosted move needs, without becoming a private hosted-only escape hatch.
 */
export interface AtlasBundle {
  contractVersion: typeof CONTRACT_VERSION;
  kind: "atlas-bundle";
  manifest: BundleManifest;
  landscape: LandscapeDocument;
  createdAt?: string | null;
  generator?: Generator | null;
  compatibility?: BundleCompatibility | null;
  workspace?: WorkspaceMetadata | null;
  diagrams?: Diagram[];
  attachments?: AttachmentManifestEntry[];
  modules?: ModuleData[];
  restore?: RestoreHints | null;
  excluded?: ExcludedCategory[];
}
