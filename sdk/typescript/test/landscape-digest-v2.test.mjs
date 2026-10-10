import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createPublicKey, verify } from "node:crypto";
import { test } from "node:test";
import { validateLandscapeDigestV2, LANDSCAPE_DIGEST_V2_VERSION, CONTRACT_VERSION } from "../dist/index.js";

const read = name => JSON.parse(readFileSync(new URL(`../../../conformance/samples/landscape-digest-v2.${name}.json`, import.meta.url), "utf8"));
// Native JSON.stringify supplies ECMAScript number/string serialization; sort by UTF-16.
const canonical = value => value === null || typeof value !== "object" ? JSON.stringify(value)
  : Array.isArray(value) ? `[${value.map(canonical).join(",")}]`
  : `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
const verifies = wrapper => verify("sha256", Buffer.from(canonical(wrapper.digest)),
  createPublicKey({ key: Buffer.from(wrapper.signature.publicKey, "base64url"), format: "der", type: "spki" }),
  Buffer.from(wrapper.signature.value, "base64url"));

for (const name of ["valid", "without-relations", "omitted-consent", "stage2"]) {
  test(`semantic positive: ${name}`, () => assert.deepEqual(validateLandscapeDigestV2(read(name)), []));
}
for (const [name, error] of [
  ["dangling-ref", "dangling-ref"], ["self-edge", "self-edge"], ["duplicate-edge", "duplicate-edge"],
  ["reverse-integration", "duplicate-edge"], ["duplicate-ref", "duplicate-ref"],
  ["wrong-vendor-target", "invalid-relation-endpoints"], ["wrong-platform-target", "invalid-relation-endpoints"],
  ["outside-scope", "item-outside-scope"], ["relations-off", "relations-not-enabled"],
  ["relations-omitted-consent", "relations-not-enabled"]
]) {
  test(`semantic negative: ${name}`, () => assert.ok(validateLandscapeDigestV2(read(name)).includes(error)));
}
test("schema versions coexist", () => {
  assert.equal(CONTRACT_VERSION, "1");
  assert.equal(LANDSCAPE_DIGEST_V2_VERSION, "2");
});
test("edge count limit is inclusive", () => {
  const digest = read("valid");
  digest.relations = [];
  for (let i = 0; i < 20000; i++) {
    digest.items.push({ ref: `p${i}`, kind: "platform", name: "Example", lifecycle: "active" });
    digest.relations.push({ from: "c1", to: `p${i}`, kind: "runsOn" });
  }
  assert.deepEqual(validateLandscapeDigestV2(digest), []);
  digest.items.push({ ref: "overflow", kind: "platform", name: "Example", lifecycle: "active" });
  digest.relations.push({ from: "c1", to: "overflow", kind: "runsOn" });
  assert.deepEqual(validateLandscapeDigestV2(digest), ["too-many-relations"]);
});
test("JCS signed vector verifies", () => {
  const wrapper = read("signed.valid");
  assert.equal(wrapper.signature.algorithm, "rsa-pkcs1-v1_5-sha256");
  assert.equal(wrapper.signature.keyId, "atlas-conformance-test-key");
  assert.ok(verifies(wrapper));
});
test("changed edge fails signature verification", () => assert.equal(verifies(read("signed.tampered")), false));
for (const field of ["edge", "ref", "layer", "scope"]) {
  test(`signature covers ${field}`, () => {
    const wrapper = read("signed.valid");
    if (field === "edge") wrapper.digest.relations[0].from = "c4";
    if (field === "ref") wrapper.digest.items[3].ref = "new-ref";
    if (field === "layer") wrapper.digest.items[0].layer = "data";
    if (field === "scope") wrapper.digest.scope.relations = false;
    assert.equal(verifies(wrapper), false);
  });
}
test("signature is independent of root property order and formatting", () => {
  const wrapper = read("signed.valid");
  wrapper.digest = Object.fromEntries(Object.entries(wrapper.digest).reverse());
  assert.ok(verifies(JSON.parse(JSON.stringify(wrapper, null, 4))));
});
test("JCS Unicode vector matches .NET", () => {
  assert.equal(canonical({ z: "æ漢😀", a: 1.0 }), '{"a":1,"z":"æ漢😀"}');
});
