import type { Asset, ImportAsset, InUsePeriod } from "../src/index.js";
const period: InUsePeriod = { plannedStart: "2027-01-01", actualEnd: null };
const asset: Asset = { id: "a", kind: "system", name: "A", lifecycle: "active", inUsePeriod: period };
const imported: ImportAsset = { externalId: "a", kind: asset.kind, name: asset.name, lifecycle: asset.lifecycle, inUsePeriod: period };
// @ts-expect-error Calendar dates are strings, not Date objects.
const invalid: InUsePeriod = { actualStart: new Date() };
void imported; void invalid;
