/**
 * Public data model for VEV Atlas. Catalogue concepts only — no integration-criticality, EOL risk,
 * portfolio scoring or AI review (those work with the data and live in the private Atlas core).
 * See handbook 11 §1. Mirrors the authoritative JSON Schemas in `schemas/v1`.
 */

/** The atlas-contracts schema major version. */
export const CONTRACT_VERSION = "1" as const;

export type AssetKind = "system" | "application" | "server" | "infrastructure";

export type Lifecycle = "draft" | "active" | "retired";

export type RelationshipType =
  | "runs-on"
  | "hosts"
  | "connects-to"
  | "depends-on"
  | "part-of";

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
