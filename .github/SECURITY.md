# Security policy

## Reporting a vulnerability

**Please report security issues privately — never through a public issue, pull
request or discussion.**

Use GitHub's private vulnerability reporting for this repository:

1. Go to the **Security** tab of `Vev-software/atlas-contracts`.
2. Choose **Report a vulnerability** to open a private advisory visible only to
   you and the maintainers.

If you cannot use private reporting, contact the Atlas maintainers through the
Vev-software security policy channel rather than filing anything public.

Please include enough detail to reproduce: the affected schema, SDK or version,
a minimal payload or snippet, and the impact you observed. We will acknowledge
your report, keep you updated while we investigate, and coordinate disclosure
with you once a fix is available.

## Scope

This repository publishes the public Atlas data-model and interop **contracts**:
JSON Schemas and the .NET / TypeScript SDKs generated from them. It carries no
runtime, no analysis logic and no secrets. Relevant reports include, for example,
a schema that fails to constrain a documented invariant, or an SDK that produces
or accepts a payload that does not match the published schema.

The Atlas runtime lives in separate repositories and has its own security policy;
please report runtime issues there.
