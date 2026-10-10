import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { validateInUsePeriod } from "../dist/index.js";

test("unknown, partial and same-day dates are valid", () => {
  for (const period of [undefined, null, {}, {plannedStart: null}, {actualEnd: "2028-02-29"},
    {plannedStart: "2027-01-01", plannedEnd: "2027-01-01"},
    {plannedStart: "2029-01-01", actualStart: "2027-01-01"}])
    assert.deepEqual(validateInUsePeriod(period), []);
});

test("reject invalid dates in every boundary without timezone normalization", () => {
  for (const key of ["plannedStart", "plannedEnd", "actualStart", "actualEnd"])
    for (const value of ["2027-02-29", "1900-02-29", "2027-13-01", "2027-01-00", "2027-1-1", "0000-01-01", "2027-01-01T00:00:00Z", 12])
      assert.equal(validateInUsePeriod({[key]: value}).length, 1);
  for (const value of ["0001-01-01", "2000-02-29", "9999-12-31"])
    assert.deepEqual(validateInUsePeriod({actualStart: value}), []);
});

test("planned and actual pair ordering are checked separately", () => {
  assert.equal(validateInUsePeriod({plannedStart: "2029-01-01", plannedEnd: "2027-01-01",
    actualStart: "2029-01-01", actualEnd: "2027-01-01"}).length, 2);
});

test("import, export, portable bundle and embedded target assets preserve all four dates", () => {
  const period = { plannedStart: "2027-01-01", plannedEnd: "2029-01-01",
    actualStart: "2027-02-15", actualEnd: "2029-03-01" };
  const read = name => JSON.parse(readFileSync(new URL(`../../../conformance/samples/${name}.sample.json`, import.meta.url), "utf8"));
  for (const name of ["landscape", "import", "bundle", "target-architecture"]) {
    const document = read(name);
    const getAsset = value => name === "bundle" ? value.landscape.assets[0]
      : name === "target-architecture" ? value.versions[0].assetChanges[0].asset : value.assets[0];
    const asset = getAsset(document);
    const lifecycle = asset.lifecycle;
    asset.inUsePeriod = period;
    const restored = getAsset(JSON.parse(JSON.stringify(document)));
    assert.deepEqual(restored.inUsePeriod, period);
    assert.deepEqual(validateInUsePeriod(restored.inUsePeriod), []);
    assert.equal(restored.lifecycle, lifecycle);
  }
});
