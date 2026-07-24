# ADR 0016: AI-assisted but human-approved content curation

- Status: Proposed
- Date: 2026-07-25

## Context

Structured learner feedback may accumulate faster than maintainers can manually group and summarize it. AI can assist with duplicate suggestions, categorization, impact estimates, staleness checks, and draft proposals, but it is not competent evidence of linguistic correctness, contributor permission, licensing, privacy compliance, or release approval.

Feedback text is untrusted input and may contain prompt injection, secrets, personal information, or malicious content.

## Decision

A weekly and manually dispatchable, review-only workflow may process validated, consented, and redacted public feedback.

Before a model call, deterministic steps will reject malformed, oversized, versionless, unconsented, or prohibited records; identify exact duplicates; and resolve current content revisions. The model may propose probable duplicate groups, categories, confidence, impact, and human-readable next actions.

Each run will record the provider, model, prompt-template version and hash, tool version, run ID, timestamp, redaction profile, and input/output digests. The model will receive no GitHub credentials or repository write tools.

AI output is evidence or a proposal only. It may not:

- merge changes;
- mark content as human reviewed or release approved;
- approve Japanese, pronunciation, audio quality, licensing, privacy, or security;
- change contributor declarations, licences, rights metadata, reviewer qualifications, review events, or release approvals;
- apply ordinary automation-ready labels to human-gated content.

Scheduled triage may create or update a draft triage report through a separate minimal-permission writer step. A draft issue or content pull request requires a human-applied approval label or manual dispatch. Generated content pull requests remain draft and retain AI provenance.

## Consequences

- Maintainers can receive bounded summaries without delegating approval authority.
- Linguistic, audio, licensing, privacy, and release gates remain human responsibilities.
- Workflow permissions, model budgets, input limits, prompt versions, and audit retention must be governed.
- AI-generated wording retains licensing and quality uncertainty until an authorized human adopts and reviews it.

## Rejected alternatives

- **Autonomous merging or review approval:** rejected because model output cannot satisfy specialist or rights review.
- **Sending all issue text directly to a model:** rejected because deterministic validation, consent, and redaction must precede processing.
- **Giving the model repository credentials:** rejected because proposal generation does not require write authority.

