# ADR 0012: Community-maintained declarative language-pack content

- Status: Proposed
- Date: 2026-07-25

## Context

TomeOfTongues intends to accept community contributions to lesson content and audio while preserving the local-first architecture, declarative `.totlang` packages, competent human review, and explicit provenance and licensing. Community workflows must not introduce executable language-specific plug-ins or make generic projects depend on `TomeOfTongues.Language.*`.

GitHub Discussions and issue comments are useful for coordination, but neither provides the exact, reviewable content revision or durable rights evidence needed for a release.

## Decision

Normal pull requests will be the authoritative path for lesson, correction, metadata, and audio-manifest contributions.

- Corrections may begin directly as pull requests.
- New lessons and coordinated recording campaigns should begin with structured GitHub Issue Forms to prevent duplicated effort, then move to pull requests.
- Generated recording kits will identify exact prompt IDs, scripts, readings, filenames, technical requirements, and the required contribution manifest.
- Authoring sources, contribution records, review events, and release approvals will remain declarative and versioned.
- GitHub Discussions may support community coordination but will not be an authoritative rights, review, or release record.
- Only content that passes the applicable human and rights gates may be packaged for release.

## Consequences

- Community work remains reviewable through ordinary Git history and pull-request controls.
- Contributors receive reproducible authoring and recording inputs rather than relying on informal instructions.
- Content provenance can be traced from a released pack to an exact contribution and review record.
- Maintainers must provide validation tooling, templates, and contributor guidance.
- Contribution throughput is intentionally limited by human review capacity.

## Rejected alternatives

- **Executable language-specific plug-ins:** rejected because they conflict with the declarative package boundary and generic-project independence.
- **Issue comments or Discussions as the authoritative contribution:** rejected because they do not bind rights and review evidence to exact content revisions.
- **Direct commits to the release branch:** rejected because they bypass contribution and specialist review gates.

