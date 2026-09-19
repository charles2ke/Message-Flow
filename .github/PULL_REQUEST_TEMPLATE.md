## Summary

<!-- What changes and why. Link the issue this closes, if any. -->

## Ports touched

- [ ] C# (`src/MessageFlow`)
- [ ] Java (`java/`)
- [ ] Python (`python/`)
- [ ] Node (`node/`)
- [ ] Docs / site / workflows

<!-- The ports are behaviourally equivalent. If this changes behaviour in one port only,
     explain why, or link the follow-up issue for the others. -->

## Verification

<!-- The commands you ran, and anything you checked manually. -->

## Checklist

- [ ] Tests cover the change (the C# suite enforces 100% line, branch and method coverage)
- [ ] Public API additions are documented with doc comments, and `python scripts/update_readme.py`
      was run if C# public types changed
- [ ] `CHANGELOG.md` updated under `## [Unreleased]` if consumers are affected
- [ ] No new runtime dependency was introduced in any port
- [ ] No secrets, credentials or personal data are included in the diff
