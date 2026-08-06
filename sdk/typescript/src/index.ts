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

/** The portability surface: a whole landscape carried across a boundary. */
export interface LandscapeDocument {
  contractVersion: typeof CONTRACT_VERSION;
  exportedAt?: string | null;
  assets: Asset[];
  relationships?: Relationship[];
}
