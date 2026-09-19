# Enterprise and compliance readiness

This document is the evidence pack for adopting MessageFlow in a large organisation: what the
library does and does not do, how it is built and released, and which controls a security,
procurement or compliance review usually asks about. It complements the
[security policy](SECURITY.md) and the [contribution guide](CONTRIBUTING.md).

Nothing here replaces your own risk assessment — it is a starting point that answers the recurring
questions without a support ticket.

## At a glance

| Question | Answer |
| --- | --- |
| Licence | Apache-2.0 for every port, no dual licensing, no CLA required |
| Runtime dependencies | None, in all four ports |
| Supported runtimes | .NET 8 and .NET 10, Java 17+, Python 3.9+, Node 20+ |
| Network access at runtime | None |
| Telemetry | None; observability is opt-in and writes only to sinks you provide |
| Persistence | None; no files, no configuration, no environment variables |
| Native code / P-Invoke / reflection | None |
| Thread safety | A built chain is immutable and safe to share across threads |
| Test coverage gate | 100% line/branch/method for C#, 100% coverage with branch measurement for Python; Java and Node have no enforced coverage gate |
| Source of truth | This repository; releases are built only by GitHub Actions from a `v*` tag |
| Vulnerability reporting | Private GitHub security advisory, see [SECURITY.md](SECURITY.md) |

## Licensing and intellectual property

Every port is licensed under Apache-2.0 ([LICENSE](LICENSE)), including the explicit patent grant of
section 3, which is what most enterprise legal reviews look for. .NET and npm declare the SPDX
expression `Apache-2.0` directly in their package metadata (`PackageLicenseExpression`, `license`
field); Python's package metadata uses the text license field `Apache-2.0`, and Maven exposes a
license name/URL pair (`Apache License, Version 2.0`) rather than an SPDX identifier, so a scanner
that only understands SPDX expressions may need a manual mapping for the Java artifact.

Because no port has runtime dependencies, adopting MessageFlow introduces exactly one licence into
your dependency graph. The development-time dependencies (xUnit, JUnit, pytest, ruff, TypeScript and
the GitHub Actions used in CI) are never shipped to consumers and are visible in
`tests/`, `java/pom.xml`, `python/pyproject.toml`, `node/package.json` and `.github/workflows/`.

Contributions are accepted under the inbound-equals-outbound rule of Apache-2.0 section 5; there is
no separate contributor licence agreement and no copyright assignment.

## Data handling and privacy

MessageFlow moves an application's own request and response objects between the application's own
handlers. It never inspects, copies, serialises, persists or transmits them, and it has no notion of
users, identifiers or personal data of its own. Whether a deployment acts as a GDPR controller or
processor depends entirely on how the application built with the library uses and configures it;
the library itself does not process personal data and does not determine that role.

Two optional decorators emit information, and both are off unless you enable them:

- `UseLogging` writes a plain text message — the chain name (source and destination type names) and
  the elapsed time on start/completion, or the exception on failure — to the `IChainLogger` (or
  equivalent per-port sink) that you supply. Request and response values are not included.
- `UseTracing` starts `Activity` spans (and the equivalent in the other ports). For the C# port,
  these are emitted on the library-created `ChainDiagnostics.ActivitySource`; your application
  subscribes listeners/exporters to it, it does not supply the source itself. The other ports accept
  a caller-supplied tracer interface instead. Either way, spans reach only the exporters you
  configure.

Handler exceptions propagate unchanged. If your requests carry regulated data, redact it in your own
handlers or log sinks before it reaches an exception message.

## Operational characteristics

- **Deterministic composition.** A pipeline is composed once at `Build()`; executing a request is a
  delegate invocation with no allocation for chain traversal and no locking.
- **No hidden concurrency.** The library never starts threads, timers or background work. Async
  continuations run on the scheduler your application already uses.
- **Cancellation.** Every execution path propagates the cancellation token so requests can be shed
  under load or at shutdown.
- **Air-gapped and restricted environments.** The packages are self-contained; installation from an
  internal mirror (Artifactory, Nexus, Azure Artifacts, an internal PyPI or npm proxy) needs no
  additional feeds. Nothing phones home at build or run time.
- **FIPS and hardened runtimes.** No cryptography is used, so the library imposes no constraint on a
  FIPS-validated runtime, and it runs unchanged under trimming, AOT, or a restricted security
  manager because it uses no reflection or dynamic code generation.
- **Determinism of builds.** The .NET package is built with `Deterministic`,
  `ContinuousIntegrationBuild` and SourceLink, and ships a symbol package, so a binary can be mapped
  back to the exact commit it was built from.

## Supply chain assurance

Controls that run automatically in this repository:

| Control | Where | Scope |
| --- | --- | --- |
| CodeQL, `security-extended` queries | `.github/workflows/codeql.yml` | C#, Java, Python, TypeScript |
| Dependency review on pull requests | `.github/workflows/dependency-review.yml` | fails on any known-vulnerable dependency |
| OpenSSF Scorecard | `.github/workflows/scorecard.yml` | weekly, results published to code scanning |
| Vulnerable package audit | `.github/workflows/ci.yml` | `dotnet list package --vulnerable --include-transitive` |
| Automated dependency updates | `.github/dependabot.yml` | GitHub Actions, NuGet, npm, Maven and pip, weekly |
| 100% coverage gate | `.github/workflows/ci.yml`, `.github/workflows/python-ci.yml` | C# enforces 100% line/branch/method; Python enforces 100% coverage with branch measurement enabled. Java and Node have no coverage gate |
| SBOM per release | `.github/workflows/release.yml` | SPDX document attached to the workflow run |
| Build provenance attestation | `.github/workflows/release.yml` | signed SLSA provenance for the .NET, Java and Python artifacts |
| npm provenance | `.github/workflows/release.yml` | `npm publish --provenance`, visible on the npm package page |

All workflows declare a read-only default `permissions` block and elevate only the job that needs
it. Releases are triggered exclusively by pushing a `v*` tag and every publish step is skipped when
its registry credential is absent, so a fork or a mis-triggered run cannot publish.

### Verifying what you consume

```bash
# .NET (.nupkg), Java (.jar) or Python (sdist/wheel) artifact downloaded from its registry, using
# the build provenance attestation created by actions/attest-build-provenance
gh attestation verify <artifact> --repo charles2ke/Message-Flow

# npm package: verify the registry-signed provenance attestation instead
npm audit signatures
```

The SPDX SBOM for a release is attached to the corresponding run of the release workflow and can be
downloaded with `gh run download`. The .NET symbol package on NuGet plus SourceLink lets you step
into the exact sources that produced the binary.

## Governance and support

- **Maintenance model.** Maintainers are listed in [.github/CODEOWNERS](.github/CODEOWNERS); changes
  reach `main` through reviewed pull requests that must pass the full matrix of checks.
- **Versioning.** Semantic Versioning. Breaking changes to a public API only happen in a major
  release and are listed in [CHANGELOG.md](CHANGELOG.md); the public API surface of the C# port is
  regenerated into the README on every change, so API drift is visible in the diff.
- **Release cadence.** Releases are cut on demand from `main`; a release can publish any subset of
  the four ports, since each publish job is skipped when its registry credential is absent.
- **Support channels.** See [SUPPORT.md](SUPPORT.md). There is no commercial support contract; the
  project is maintained on a best-effort basis under the response targets in
  [SECURITY.md](SECURITY.md) for security reports.
- **Conduct.** [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) (Contributor Covenant 2.1) applies to every
  project space.

## Frameworks this evidence maps to

The controls above are the ones typically requested for:

- **SOC 2 / ISO 27001 vendor reviews** — change management (reviewed pull requests, branch
  protection), vulnerability management ([SECURITY.md](SECURITY.md) targets, Dependabot, CodeQL) and
  secure development (coverage gate, static analysis, least-privilege workflow permissions).
- **NIST SSDF (SP 800-218) and SLSA** — provenance attestations, SBOM, deterministic builds, signed
  Maven artifacts and npm provenance, tag-triggered releases from a single source of truth.
- **EU Cyber Resilience Act open-source steward expectations** — a documented coordinated disclosure
  process, a published support window, and machine-readable SBOMs per release.
- **GDPR / data protection reviews** — no personal data processed by the library itself; see
  [data handling](#data-handling-and-privacy).

If your review needs something that is not covered here, open a
[question](https://github.com/charles2ke/Message-Flow/issues/new/choose) and this document will be
extended rather than answered privately, so the next reviewer finds the answer already written down.
