# Community contribution guide

This guide expands the routes in [`CONTRIBUTING.md`](../CONTRIBUTING.md). Pull
requests are the rights-bearing contribution path because they bind a change,
its provenance, declarations, and reviews to an exact revision. Issues and
Discussions may coordinate work, but they are not contribution grants or
release records.

The linked contribution-rights and review policies are proposed. Maintainers
must obtain the required licensing/content-owner and privacy/security review
before adopting declaration wording or treating these workflows as release
approval.

## Choose the contribution path

| Contribution | Start with | Finish with |
| --- | --- | --- |
| Code or documentation | A suitable issue, or a focused pull request for a small fix | DCO-signed commits, pull request, and repository checks |
| Existing lesson correction, translation, reading, explanation, exercise, or acceptable answer | A focused pull request; use an issue first when scope needs agreement | Content declaration, provenance, Japanese review, and release gates |
| New lesson or substantial lesson expansion | A structured Issue Form when available; otherwise a non-sensitive public issue | Maintainer-provided authoring inputs followed by a pull request |
| Audio offer or coordinated recording campaign | A structured Issue Form when available; otherwise a non-sensitive public issue | Generated recording kit, audio grant, files and manifest in a pull request |
| Specialist review | The pull request for the exact revision | An attributable human review record for the qualified role |
| Product, lesson, accessibility, or audio feedback | A public feedback Issue Form when available; otherwise a non-sensitive public issue or a user-controlled local export | Maintainer triage; a later correction still uses a pull request |
| Vulnerability or privacy-sensitive report | The private route in [`SECURITY.md`](../SECURITY.md) | Private maintainer and privacy/security triage |

An issue must not contain learner answers, study history, recordings, private
imports, credentials, personal diagnostics, private contact details, or
protected course material. There is no private general-feedback service.

## Code contributions

Keep changes issue-scoped and preserve the existing project boundaries. Every
code commit must certify Developer Certificate of Origin 1.1 by including:

```text
Signed-off-by: Contributor Name <address>
```

Create signed commits with `git commit -s`. DCO is not a lesson-content
declaration and is not an audio grant. The public sign-off is durable Git
history, so use an identity and address you are prepared to publish.

Install the .NET 10 SDK selected by `global.json` and run:

```powershell
dotnet restore TomeOfTongues.NonMaui.slnf
dotnet build TomeOfTongues.NonMaui.slnf --configuration Debug --no-restore
dotnet test TomeOfTongues.NonMaui.slnf --configuration Debug --no-build --no-restore
```

For an Android-host change, also run:

```powershell
dotnet workload restore TomeOfTongues.Maui/TomeOfTongues.Maui.csproj
dotnet build TomeOfTongues.Maui/TomeOfTongues.Maui.csproj --configuration Debug --framework net10.0-android
```

## Lesson corrections, translations, and new lessons

Language packs remain declarative `.totlang` packages. Do not add executable
language plug-ins or make Core, Application, Content, Infrastructure, MAUI, or
generic tests depend on a `TomeOfTongues.Language.*` assembly, namespace, or
type. TomeOfTongues must also continue to work without SecondBrain.

Before editing:

1. For a small correction, translation, reading, explanation, exercise, or
   acceptable-answer change, identify the existing pack, course, unit, lesson,
   expression or exercise, and current revision. A direct pull request is
   appropriate when the change is unambiguous.
2. For a new lesson or coordinated larger change, first use the relevant Issue
   Form when one is available. Otherwise open a public issue without private or
   protected material and wait for maintainers to confirm scope and authoring
   inputs.
3. Preserve existing stable IDs. Assign new IDs only to new objects, never
   silently reuse an ID for different content, and increment every affected
   revision. Where the current package format has no stable leaf ID, record the
   version-scoped locator and component digest required to identify the exact
   text.

Each content pull request must provide provenance sufficient to trace the
released material:

- source origin, author or rights holder, source date, and authoritative
  reference when applicable;
- licence plus explicit modification and redistribution permission;
- requested public attribution: real name, pseudonym, or an approved anonymous
  form;
- the affected stable content IDs and revisions, or the version-scoped locator
  and digest;
- the pull request or commit and the version and digest of the content
  declaration supplied by maintainers; and
- third-party sources and material AI involvement (`none`, `assistive`,
  `generated`, or `unknown`).

Do not copy a protected course, private import, or material whose precise
licence does not allow modification, packaging, and redistribution. Being
publicly viewable, free, or educational is not permission.

The content declaration is separate from DCO. It must establish that the
contributor created the material or documented authority to submit it, declares
the applicable content licence, permits the intended editing, packaging,
attribution, and redistribution, and discloses sources and material AI
assistance. Its exact versioned wording and acceptance mechanism require human
legal review; do not substitute an improvised PR checkbox.

### Japanese review gates

Review applies to the exact current revision; a subsequent material edit makes
the earlier approval stale. Authors may not approve their own contributions.

- Readings, rōmaji, meanings, translations, explanations, exercises, and
  acceptable answers require a proficient Japanese reviewer.
- New or substantially rewritten learner-facing Japanese, naturalness,
  register, and cultural usage require a native or near-native Japanese
  reviewer.
- Source, licence, declaration, redistribution, and attribution evidence
  require a licensing/content owner.
- A maintainer confirms that all applicable current-revision reviews and
  records are complete before release approval.

Build and validate the Japanese authoring output:

```powershell
dotnet build TomeOfTongues.Language.Japanese/TomeOfTongues.Language.Japanese.csproj
dotnet run --project TomeOfTongues.Content.Tool/TomeOfTongues.Content.Tool.csproj -- validate artifacts/language-packs
dotnet test tests/TomeOfTongues.Language.Japanese.Tests/TomeOfTongues.Language.Japanese.Tests.csproj
dotnet test tests/TomeOfTongues.Architecture.Tests/TomeOfTongues.Architecture.Tests.csproj
```

## Audio offers and recordings

Start by offering to record a defined set of material through the audio Issue
Form when available, or a public coordination issue containing no recording or
private details. Do not record until a maintainer supplies a generated,
validated recording kit.

The kit is the technical source of truth. It must identify the pack and
version, exact prompt and expression IDs, approved script and readings,
required filenames, recording notes, contribution-manifest entries, and the
exact audio specification. Follow its required format or codec, sample rate,
bit depth, channel layout, level and noise limits, editing rules, and checksum
instructions. There is intentionally no guessed repository-wide numeric
specification: if any value is absent, ask for a corrected kit before recording.

An audio pull request must use a separate versioned audio grant covering
ownership of the recording and performance, authority for every speaker,
editing, segmentation, normalisation, cleanup, conversion, packaging,
modification, public redistribution, and attribution. Its contribution record
must bind the grant version and digest to the prompt IDs, files, checksums,
licence, requested attribution, source pull request, and disclosed AI or
synthetic-audio involvement.

Do not include private conversation, background music, unauthorized speakers,
or other protected material. The audio grant does not grant voice cloning,
biometric identification, speaker-model training, or general AI-training
rights.

Audio progresses through independent, revision-bound human gates:

`draft → submitted → rights review → pronunciation review → technical quality review → human reviewed → release approved`

The applicable reviewers check licensing and performance rights, Japanese
pronunciation and prosody, transcript alignment, noise, clipping, levels,
edits, encoding, filenames, and checksums. Changed audio requires renewed
review.

## Reviews, AI disclosure, and release

Reviewer roles describe responsibilities; this document does not appoint
people. A person may cover multiple roles only when maintainers have registered
that person for each role.

| Role | Routes or reviews |
| --- | --- |
| Maintainer | Code, documentation, technical coordination, complete gate set, and release decision |
| Proficient Japanese reviewer | Readings, rōmaji, meanings, translations, explanations, exercises, and answer variants |
| Native or near-native Japanese reviewer | Learner-facing Japanese, naturalness, register, cultural usage, pronunciation, and prosody |
| Audio-quality reviewer | Transcript alignment, noise, clipping, levels, edits, encoding, filenames, and checksums |
| Licensing/content owner | Sources, declarations, licences, attribution, redistribution, recording and performance rights |
| Privacy/security reviewer | Diagnostics, external submission, private reporting, personal information, and model inputs |

AI assistance is provenance, not approval. Record material AI involvement,
provider and model when known, prompt or workflow version when available, the
human adopter, affected IDs and revisions, and source feedback or issue
references. AI cannot approve language, pronunciation, audio quality, rights,
licensing, attribution, privacy, security, reviewer qualifications, review
state, or release state. It cannot replace any required human review.

Release requires complete source, licence, declaration, attribution, and
checksum evidence; every applicable qualified human approval for the exact
current revision; no unresolved blocking concern; a valid pack artifact; and
maintainer approval. Human-gated content, audio, licensing, privacy, security,
and release work must not be marked automation-ready as a substitute for those
approvals.

## Feedback and sensitive reports

Use a public Issue Form when available for non-sensitive lesson, translation,
answer, audio, accessibility, pedagogical, or technical feedback. Review the
preview before submission and remove personal or private material. The
authoritative public record is the submitted issue; a locally exported
feedback file remains under the user's control until they deliberately share
it.

TomeOfTongues is local-first. General feedback does not silently upload learner
answers, study history, recordings, private imports, paths, device identifiers,
microphone data, or diagnostics. There is no account requirement, feedback
backend, or private general-feedback inbox promised by this guide.

Report a suspected vulnerability, exposed secret, privacy incident, or
privacy-sensitive evidence through [`SECURITY.md`](../SECURITY.md), not a
public issue, pull request, or Discussion.

## Maintainer routing checklist

Maintainers:

1. distinguish coordination issues and public feedback from rights-bearing pull
   requests, and move sensitive evidence to the private security route without
   republishing it;
2. confirm scope, current IDs and revisions, and the correct authoring or
   recording kit before work begins;
3. identify the required roles without treating an author, AI output, label,
   or CODEOWNERS approval as a substitute for qualified independent review;
4. route code to DCO checks, lesson material to content-declaration and Japanese
   review, audio to audio-grant, pronunciation, alignment, and quality review,
   and data handling to privacy/security review; and
5. block release when evidence is missing, mismatched, stale, or disputed, and
   approve only the exact validated artifact and revision.

See the
[community architecture](planning/community-contribution-feedback-curation-architecture.md),
[content licensing policy](content-licensing-policy.md),
[community-content ADR](architecture/adr/0012-community-maintained-language-pack-content.md),
[separate-rights ADR](architecture/adr/0013-separate-code-lesson-and-audio-contribution-rights.md),
[structured-feedback ADR](architecture/adr/0014-structured-versioned-user-feedback.md),
[local-first-feedback ADR](architecture/adr/0015-local-first-feedback-submission.md),
[human-approved-AI ADR](architecture/adr/0016-ai-assisted-human-approved-content-curation.md),
[provider-boundary ADR](architecture/adr/0017-provider-independent-ai-review-boundary.md),
and [release-gates ADR](architecture/adr/0018-content-review-states-and-release-gates.md).
Keep each document's recorded status unchanged; these references do not turn
proposed policy into final legal advice.
