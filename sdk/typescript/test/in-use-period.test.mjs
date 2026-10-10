import test from "node:test";
import assert from "node:assert/strict";
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
