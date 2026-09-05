# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- A dependency-free Python 3.9+ implementation of the core pipeline API.
- A dependency-free Node/TypeScript implementation of the core pipeline API.
- A GitHub Pages site with a Swagger UI playground, served by the JavaScript port in the browser.
- Release automation for the Python and Node ports: pushing a `v*` tag now publishes the PyPI
  distribution and the npm package with the tag version.

### Changed

- The npm package is now published as `@charles2ke/messageflow`; npm rejects the unscoped
  `messageflow` name because the unrelated `message-flow` package already exists.
- A chain fallback is now bound to the pipeline directly, removing one delegate call per request
  that reaches the fallback.
- CI is cheaper: workflow runs are cancelled when superseded, the per-port workflows only run when
  their directory changes, dependencies are cached, and the README job reuses the coverage artifact
  instead of running the test suite a second time.
- The landing page language cards of the site are generated from `docs/public/languages.js`, the
  same catalogue the playground serves.

## [1.0.0] - 2026-08-28

### Added

- Async-first, immutable Chain of Responsibility pipelines for .NET 8.
- Reusable, inline, middleware-style, branching, and composable handlers.
- Optional fallbacks, structured logging, tracing, and cancellation support.
- A dependency-free Java 17 implementation of the core pipeline API.

[Unreleased]: https://github.com/charles2ke/Message-Flow/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/charles2ke/Message-Flow/releases/tag/v1.0.0
