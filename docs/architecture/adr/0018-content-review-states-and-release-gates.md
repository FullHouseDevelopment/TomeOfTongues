# ADR 0018: Content and audio review states and release gates

- Status: Proposed
- Date: 2026-07-25

## Context

A mutable `reviewed` boolean cannot identify who reviewed which revision, what qualification they held, which review they performed, whether concerns remain, or whether later changes invalidated the approval. Lesson and audio contributions also require different specialist reviews before release.

AI involvement must be recorded without presenting AI assistance as a lifecycle state or evidence of review.

## Decision

Lesson content will use the lifecycle:

`draft → submitted → awaitingLinguisticReview → humanReviewed → releaseApproved`

Audio will use the lifecycle:

`draft → submitted → rightsReviewPending → pronunciationReviewPending → qualityReviewPending → humanReviewed → releaseApproved`

Both lifecycles permit `changesRequested` from review states and `deprecated` for released material. Exact transition rules will be validated rather than inferred from labels.

AI involvement is an orthogonal field with values `none`, `assistive`, `generated`, or `unknown`.

Review and release evidence will be append-only:

- `ReviewEvent` records subject, prior and new state, reviewer, qualified role, review type, date, exact revision or hash, result, concerns, and GitHub evidence.
- `ReleaseApproval` records pack version and checksum, required gate results, approver, date, and any superseded release.

Required roles are:

- maintainer;
- proficient Japanese reviewer;
- native or near-native Japanese reviewer;
- audio-quality reviewer;
- licensing/content owner;
- privacy/security reviewer.

One person may satisfy multiple roles only when registered for each role. An author may not approve their own contribution. Approvals tied to stale revisions or hashes do not satisfy a release gate.

Release approval requires complete source, licence, declaration, attribution, and checksum records; all applicable current-revision specialist reviews; and no unresolved blocking concerns. New or substantially rewritten learner-facing Japanese, register, cultural usage, pronunciation, and prosody require native or near-native review. Readings, rōmaji, meanings, explanations, exercises, and acceptable answers require proficient Japanese review. Audio also requires pronunciation, transcript-alignment, and technical quality review.

GitHub enforcement will combine CODEOWNERS with a protected named reviewer-role registry and a required review-gate check. The check will inspect changed paths, current-head approvals, reviewer qualifications, contribution records, self-review, and unresolved concerns. CODEOWNERS alone is not considered sufficient for multi-role approval.

## Consequences

- Every approval is attributable to a qualified human and an exact content revision.
- Subsequent edits automatically require renewed applicable review.
- Release orchestration can be verified deterministically.
- Maintainers must appoint qualified reviewers and protect the reviewer registry, declarations, workflows, and approval records.
- Some contributions may remain pending when specialist review capacity is unavailable.

## Rejected alternatives

- **A single `reviewed` boolean:** rejected because it loses reviewer, type, date, revision, and concern evidence.
- **AI-assisted draft as a reviewed state:** rejected because AI provenance is not human approval.
- **CODEOWNERS alone:** rejected because one listed code-owner approval does not enforce every specialist role.
- **Author self-approval:** rejected because it does not provide independent review.

