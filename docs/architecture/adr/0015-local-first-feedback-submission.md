# ADR 0015: Local-first feedback submission without a backend

- Status: Proposed
- Date: 2026-07-25

## Context

Learners need a convenient way to report content and product problems from the MAUI application. The initial workflow must preserve local-first privacy and must not require a TomeOfTongues service or silently transmit learner answers, study history, recordings, or imported private content.

Public GitHub submission is useful but exposes the submitter's GitHub identity and submitted text. Some reports may instead be sensitive or too large for a prefilled issue URL.

## Decision

The initial MAUI feedback flow will:

- compose the feedback draft in memory;
- derive content identifiers and versions from the installed package;
- exclude learner answers, study history, recordings, private imports, paths, device identifiers, and microphone data by default;
- keep diagnostics disabled unless the user opts in, using a reviewed allowlist;
- show a complete submission preview and a clear warning about public GitHub identity and text exposure;
- open a prefilled GitHub Issue Form or ordinary issue URL within a conservative URL-size limit;
- offer explicit JSON export when public submission is unsuitable or the size limit is exceeded;
- clear in-memory draft and temporary diagnostics on cancellation;
- retain an exported file only because the user explicitly requested it.

The MVP will not include a TomeOfTongues feedback backend, direct GitHub API authentication, background submission, clipboard fallback, or automatic upload of attachments.

Security and privacy vulnerabilities should use GitHub private vulnerability reporting after maintainers enable and document it. General private feedback remains export-only initially.

A future `IPrivateFeedbackTransport` boundary may be defined, but no implementation will ship until retention, authentication, encryption, deletion, operator access, jurisdiction, and privacy-notice decisions are approved.

## Consequences

- Feedback composition remains useful offline and under user control.
- Public exposure is visible before the browser opens.
- Users without GitHub or with sensitive reports can keep or share an export through a channel they choose.
- Issue Form URL behavior and limits require a tested fallback.
- General private submission is intentionally incomplete in the MVP.

## Rejected alternatives

- **A required first-party backend:** rejected because it adds data custody and conflicts with the initial local-first goal.
- **Automatic diagnostic or audio upload:** rejected because it creates unnecessary privacy risk.
- **Silent truncation or clipboard copying:** rejected because the preview would no longer match what is disclosed or submitted.

