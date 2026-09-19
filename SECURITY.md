# Security Policy

## Supported versions

MessageFlow follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Security fixes are
released for the latest minor release of the current major version, and for the last minor release
of the previous major version for six months after a new major version ships.

| Version | Supported |
| --- | --- |
| 1.x (latest minor) | :white_check_mark: |
| 1.x (older minors) | :x: — upgrade to the latest 1.x patch |

All four ports (`MessageFlow` on NuGet, `io.github.charles2ke:messageflow` on Maven Central,
`messageflow` on PyPI and `@charles2ke/messageflow` on npm) share the version number and are patched
together, even when only one port is affected.

## Reporting a vulnerability

Report suspected vulnerabilities privately through GitHub:
[**Report a vulnerability**](https://github.com/charles2ke/Message-Flow/security/advisories/new).
Do not open a public issue, pull request or discussion for a suspected vulnerability.

Please include, as far as you can determine them:

- the affected port and version, and the runtime (.NET, JVM, Python or Node) it was observed on;
- a description of the impact and the smallest reproducer you have;
- any suggested mitigation.

### What to expect

| Stage | Target |
| --- | --- |
| Acknowledgement of the report | 3 business days |
| Initial assessment and severity (CVSS v3.1) | 10 business days |
| Status updates while the report is open | every 10 business days |
| Fix or documented mitigation for accepted reports | 90 days, sooner for high and critical severity |

Accepted reports are fixed on a private fork, released as a patch version of every affected port and
published as a GitHub Security Advisory with a CVE requested through GitHub. Reporters are credited
in the advisory unless they ask not to be. Declined reports are closed with an explanation; if you
disagree with the assessment, reply on the advisory and it will be reconsidered.

Please give the maintainers the disclosure window above before publishing details. There is no bug
bounty for this project.

## Scope

In scope: the library code of the four ports in `src/`, `java/`, `python/` and `node/`, the release
workflows that publish them, and the published packages themselves.

Out of scope: the documentation site in `docs/` (a static page that runs the JavaScript port
entirely in the browser and stores no data), the samples and benchmarks, vulnerabilities that
require an attacker to already control the handlers composed into a chain, and findings that only
apply to unsupported versions.

## Security properties of the library

MessageFlow composes handler delegates; it does not parse untrusted input, perform I/O, spawn
processes, or read configuration or environment variables. Every port ships with zero runtime
dependencies, so the transitive attack surface of adopting it is the library itself.

- Requests and responses are passed through unchanged; nothing is copied, cached or serialised.
- No telemetry is collected and nothing is sent over the network. The optional `UseLogging` and
  `UseTracing` decorators only emit to the sink or `ActivitySource` the application provides, and
  what they emit is described in [ENTERPRISE.md](ENTERPRISE.md#data-handling-and-privacy).
- A chain is immutable once built and safe to share across threads; handlers supplied by the
  application must be thread-safe themselves.
- Exceptions thrown by handlers propagate unchanged, including their messages and stack traces, so
  applications remain responsible for redacting anything sensitive before it reaches a log sink.

## Security assurance in the pipeline

Every push and pull request runs CodeQL (`security-extended`) over the C#, Java, Python and
TypeScript sources, dependency review, OpenSSF Scorecard, and a vulnerable-package audit per
ecosystem. Releases are built by GitHub Actions with pinned workflow permissions, and ship an SBOM
and a signed build provenance attestation. See
[ENTERPRISE.md](ENTERPRISE.md#supply-chain-assurance) for the full list and for how to verify the
artifacts you consume.
