# ADR 0013: Separate code, lesson, and audio contribution rights

- Status: Proposed
- Date: 2026-07-25

## Context

Code, lesson prose, translations, exercises, readings, recorded performances, and edited audio involve different rights and attribution requirements. A code sign-off does not by itself grant the permissions needed to redistribute, adapt, encode, package, and attribute lesson or audio material.

The project needs durable, machine-readable evidence for each accepted contribution without requiring copyright assignment or silently acquiring unrelated rights.

## Decision

The repository will distinguish three contribution-rights paths:

1. Code contributions require Developer Certificate of Origin sign-off and remain subject to the repository's approved code licence.
2. Lesson-content contributions require a separate, versioned content declaration covering original prose, translations, exercises, readings, notes, adaptations, redistribution, modification, and attribution under the approved content licence.
3. Audio contributions require a separate, versioned audio declaration covering the recording, performance, edits, format conversion, packaging, redistribution, modification, attribution, and applicable voice, publicity, privacy, or personality permissions.

Accepted lesson and audio material is intended to use CC BY-SA 4.0, subject to human legal approval. Code and content licence notices must remain visibly distinguishable.

Each contribution record will identify the contributor, optional public display name or pseudonym, declaration version and digest, licence, requested attribution, affected content revisions or assets, pull request or commit, and disclosed AI involvement.

The project will not require copyright assignment. Lesson or audio declarations will not grant AI-training or voice-cloning rights; any such use would require a separate future opt-in.

## Consequences

- DCO sign-off cannot be mistaken for lesson or audio permission.
- Released content can carry accurate provenance and attribution.
- Contributors may choose an attribution identity compatible with the applicable licence and legal obligations.
- Legal review is required before declaration text is adopted, including treatment of minors, moral rights, performance rights, personality rights, takedowns, and the exact code-licence identifier.
- Contribution tooling and validation must reject missing, stale, or mismatched declaration evidence.

## Rejected alternatives

- **One declaration for every contribution type:** rejected because it obscures materially different rights.
- **Copyright assignment:** rejected as unnecessarily broad for the intended community model.
- **Implicit permission through pull-request submission:** rejected because it is not sufficiently explicit or content-specific.
- **Bundled AI-training or voice-cloning consent:** rejected because those uses are outside the contribution purpose and require separate informed consent.

