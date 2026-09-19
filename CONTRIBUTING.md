# Contributing to MessageFlow

Thanks for taking the time to contribute. This guide describes how to get a change from a local
clone into a release, and which checks it has to pass on the way.

By contributing you agree that your contribution is licensed under the
[Apache-2.0 licence](LICENSE) of this project (Apache-2.0 section 5). There is no separate
contributor licence agreement. All project spaces are governed by the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- **Bugs and features**: open an [issue](https://github.com/charles2ke/Message-Flow/issues/new/choose)
  first for anything that changes behaviour or the public API, so the design can be agreed before
  the code is written. Small fixes (typos, documentation, obvious bugs) can go straight to a pull
  request.
- **Security issues**: do not open an issue — follow [SECURITY.md](SECURITY.md).
- **Scope**: MessageFlow stays small and dependency-free. Proposals that add a runtime dependency to
  any port, or that move behaviour out of the composition step into per-request work, are unlikely
  to be accepted.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/MessageFlow` | C# library (`net8.0`, `net10.0`) |
| `tests/MessageFlow.Tests` | C# test suite, 100% coverage gate |
| `java/` | Java 17 port (Maven, `io.github.charles2ke:messageflow`, package `io.github.messageflow`) |
| `python/` | Python 3.9+ port (src layout, package `messageflow`) |
| `node/` | Node 20+ / TypeScript port (`@charles2ke/messageflow`) |
| `docs/` | GitHub Pages site and Swagger UI playground |
| `samples/`, `benchmarks/` | Runnable samples and BenchmarkDotNet benchmarks |
| `scripts/update_readme.py` | Regenerates the auto-generated README sections |

## Building and testing

Run the checks for every port you touched. CI runs the same commands.

```bash
# C#
dotnet build MessageFlow.slnx
dotnet format MessageFlow.slnx --verify-no-changes
dotnet test tests/MessageFlow.Tests/MessageFlow.Tests.csproj \
  -p:CollectCoverage=true -p:Threshold=100 -p:ThresholdType="line%2cbranch%2cmethod"

# Java
(cd java && mvn verify)

# Python
(cd python && pip install -e ".[dev]" && ruff check src tests && pytest --cov=messageflow --cov-fail-under=100)

# Node
(cd node && npm ci && npm test)

# Documentation site
(cd docs && npm ci && npm run build)
```

Comma-separated MSBuild `-p` values must be escaped as `%2c` in a shell, as above.

If you added or changed a public C# type, regenerate the README API table:

```bash
python scripts/update_readme.py
```

## Coding conventions

- Keep the four ports behaviourally equivalent. A change to the composition rules of one port should
  be mirrored in the others in the same pull request, or an issue should be opened for the rest.
- Follow the existing style of the file; `.editorconfig` and `dotnet format` are authoritative for
  C#, `ruff` for Python, `-Xlint:all -Werror` for Java and the TypeScript compiler options for Node.
- Warnings are errors. Do not suppress an analyser without a comment explaining why.
- Public API additions need XML doc comments (C#), Javadoc (Java), docstrings (Python) and TSDoc
  (Node), because the documentation is generated from them.
- New code needs tests: the C# suite enforces 100% line, branch and method coverage and the Python
  suite 100% line coverage, so an untested branch fails the build.

## Pull requests

1. Branch from `main` and keep the change focused; unrelated fixes belong in their own pull request.
2. Use [Conventional Commits](https://www.conventionalcommits.org/) for commit messages
   (`feat:`, `fix:`, `docs:`, `ci:`, `build:`, `test:`, `refactor:`, `perf:`, `chore:`), which is
   also what the Dependabot configuration produces.
3. Add an entry under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) for anything a consumer
   would notice.
4. Fill in the pull request template, including how you verified the change.
5. Every required check must pass, and a maintainer review is required before merge. Merges to
   `main` are squashed.

## Releasing

Releases are cut by maintainers only. Pushing a full semver tag (for example `v1.2.3`) runs `.github/workflows/release.yml`, which publishes all four ports under that
version, attaches an SBOM and produces build provenance attestations. See the *Releases and
versioning* section of the [README](README.md#releases-and-versioning).
