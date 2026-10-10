# Asset in-use period

`Asset` and `ImportAsset` optionally carry `inUsePeriod`, for all asset kinds.
It is held catalogue metadata, not vendor product EOL, analysis or an event log.

```json
{
  "plannedStart": "2027-01-01",
  "plannedEnd": "2029-01-01",
  "actualStart": "2027-02-15"
}
```

The four optional fields are calendar dates (`YYYY-MM-DD`, years 0001 through 9999),
without a time or timezone. A start is the first day in use (inclusive); an end
is the first day out of use (exclusive). Equal boundaries are permitted and
represent an empty interval. Missing or null dates mean unknown, not proof of
unbounded use. A missing/null period or empty object provides no date information.

For each planned/actual pair, start must be on or before end when both are known.
There is no ordering constraint between planned and actual dates: actual outcomes
may precede or follow a plan. Dates neither infer nor change `lifecycle`. Future
actual dates are not rejected by this portable contract; recording policy belongs
to the product. Only one planned and one actual interval are represented; multiple
use episodes, record-time history and historical snapshots are outside this step.

Validate raw JSON with the published schema, with `format: date` assertions enabled,
before SDK deserialization (otherwise unknown fields can be discarded). JSON Schema
cannot compare dates: also run .NET `InUsePeriodValidation.Validate(period)` or
TypeScript `validateInUsePeriod(period)` for every asset in import/export, the
bundle landscape and embedded Target Architecture assets. The TypeScript helper
also validates calendar dates; .NET `DateOnly` rejects invalid calendar dates while
parsing. An empty error list means the period's semantic rules passed, not that
the whole document or an import operation was accepted.

Importers/exporters must preserve all four dates independently. Keep planned dates
when actual dates become known; actual outcomes do not overwrite the plan. The landscape and
portable bundle reuse `Asset`; Target Architecture embedded assets do too. The
contract does not implement runtime storage, merge policies, UI or time filtering.

## Compatibility and release

This is an additive optional field within catalogue contract v1; `contractVersion`
remains `"1"`. Existing documents without the period remain valid. Older closed
schemas reject documents containing the new field; older SDKs may discard it.
Consumers needing these dates must upgrade and pin the package release containing
this change. Package versions are tag-derived, independent of document majors.
Release/publication follows `releasing.md` after merge; publication is not part
of this issue's implementation acceptance.

Landscape share digest v1/v2 remain unchanged and reject `inUsePeriod`. Sharing
these dates needs a separate disclosure and versioning decision. Digest v2 was
already delivered by #45; this change does not reopen it or create a digest version.
Snapshots, as-of metadata and content hashes are deferred to a separate design;
these intervals cannot reconstruct past names, relationships or other metadata.
