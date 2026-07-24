# ADR 0014: Structured versioned user feedback

- Status: Proposed
- Date: 2026-07-25

## Context

Feedback must remain interpretable after a language pack changes. Free-form reports that omit package versions, stable content targets, or revisions cannot reliably identify what the learner saw. `.totlang` v1 has stable identifiers for major objects but not every feedback-addressable leaf.

The feedback contract must remain language-neutral and cover linguistic, pedagogical, audio, accessibility, privacy, and technical concerns.

## Decision

Define a versioned `FeedbackRecord` contract whose first version contains:

- a schema version, UUIDv7 feedback identifier, and creation time;
- pack ID, `.totlang` schema version, package version, package checksum, and relevant content revisions;
- a target kind and stable target-ID chain;
- a version-scoped legacy locator and component digest when a stable leaf ID is unavailable;
- a defined feedback category;
- the observation, expected behavior or proposed correction, and optional context;
- diagnostic-inclusion and redaction summaries;
- explicit public-submission and AI-processing consent fields.

Supported targets will include packs, courses, lessons, steps, expressions, representations, translations, exercises, acceptable answers, explanations, audio assets, and user-interface behavior. Categories will include unnatural wording, incorrect meaning, reading or transliteration errors, grammar explanations, cultural or register concerns, pronunciation, audio quality, exercise ambiguity, difficulty, accessibility, technical defects, privacy concerns, and other.

Future `.totlang` schema work will add stable IDs and revisions to every feedback-addressable leaf. Legacy v1 reports will use the nearest stable parent together with the exact package version, package SHA-256, version-scoped path, and component digest. A legacy index or path will never be represented as stable across package versions.

For public reports, the canonical record will be the validated GitHub issue together with its normalized machine-readable representation. Exported JSON remains user-owned until deliberately submitted.

## Consequences

- Reports can be compared with current content and identified as current, stale, or ambiguous.
- Deterministic validation and exact duplicate detection can occur before AI processing.
- Schema evolution and legacy packages require explicit compatibility rules.
- Content-schema work must coordinate stable leaf IDs before `.totlang` v2 is frozen.

## Rejected alternatives

- **Free-form issue text only:** rejected because it cannot reliably bind feedback to the content shown.
- **A single lesson ID without version or checksum:** rejected because later edits would make reports ambiguous.
- **Device or learner-history identifiers as correlation keys:** rejected because they are unnecessary and conflict with local-first privacy.

