# Contributing to TomeOfTongues

Thank you for helping with TomeOfTongues. This is the authoritative starting
point for code, documentation, lesson, translation, acceptable-answer, audio,
review, and feedback contributions.

The detailed workflows are in the
[community contribution guide](docs/community-contribution-guide.md). Security
vulnerabilities and privacy-sensitive reports follow [SECURITY.md](SECURITY.md);
do not put sensitive evidence in a public issue or pull request.

The contribution-rights and review policies linked below are proposed and
still require the stated human approvals. This guide does not adopt final legal
language or appoint reviewers.

## Find suitable work

Browse issues labelled
[`good first issue`](https://github.com/yaron-E92/TomeOfTongues/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22good%20first%20issue%22)
or
[`help wanted`](https://github.com/yaron-E92/TomeOfTongues/issues?q=is%3Aissue%20state%3Aopen%20label%3A%22help%20wanted%22).
Comment before starting larger work so that maintainers can confirm scope and
avoid duplicate effort. Keep each pull request focused on one issue.

Use a pull request for code, documentation, lesson corrections, translations,
acceptable answers, lesson material, and audio files or manifests. A pull
request is the authoritative, revision-specific contribution path.

Use an available GitHub Issue Form to propose a new lesson, coordinate a
recording campaign, or submit non-sensitive public feedback. Until a suitable
form exists, open an ordinary public issue containing no private data. An issue
coordinates work; it is not a rights grant, review record, or release approval.
Corrections to existing lesson content may begin directly as a pull request.

## Code and documentation

Code commits require Developer Certificate of Origin (DCO) sign-off:

```text
Signed-off-by: Contributor Name <address>
```

Use `git commit -s` to add the trailer. The sign-off and its name and address
become part of the public Git history. DCO sign-off covers the code path only;
it does not replace the separate lesson-content declaration or audio grant.

Install the .NET 10 SDK selected by `global.json`, then verify the non-MAUI
solution:

```powershell
dotnet restore TomeOfTongues.NonMaui.slnf
dotnet build TomeOfTongues.NonMaui.slnf --configuration Debug --no-restore
dotnet test TomeOfTongues.NonMaui.slnf --configuration Debug --no-build --no-restore
```

Changes affecting the Android host also require:

```powershell
dotnet workload restore TomeOfTongues.Maui/TomeOfTongues.Maui.csproj
dotnet build TomeOfTongues.Maui/TomeOfTongues.Maui.csproj --configuration Debug --framework net10.0-android
```

## Language content and audio

Read the detailed guide before changing a language pack. In summary:

- lesson prose, corrections, translations, exercises, readings, and acceptable
  answers require a separate versioned content declaration and complete
  provenance;
- audio requires a generated recording kit and a separate versioned audio
  grant; do not record before maintainers provide the exact prompts, filenames,
  and technical specification;
- preserve stable IDs, update affected revisions, and bind source, declaration,
  review, and attribution evidence to the exact revision or asset checksum;
- disclose material AI involvement; and
- obtain every applicable independent human review before release.

Build and validate the Japanese pack with:

```powershell
dotnet build TomeOfTongues.Language.Japanese/TomeOfTongues.Language.Japanese.csproj
dotnet run --project TomeOfTongues.Content.Tool/TomeOfTongues.Content.Tool.csproj -- validate artifacts/language-packs
dotnet test tests/TomeOfTongues.Language.Japanese.Tests/TomeOfTongues.Language.Japanese.Tests.csproj
dotnet test tests/TomeOfTongues.Architecture.Tests/TomeOfTongues.Architecture.Tests.csproj
```

AI may assist with a draft or review proposal, but it cannot approve language,
pronunciation, audio quality, rights, licensing, attribution, privacy,
security, reviewer status, or release state. Those decisions belong to
qualified humans reviewing the exact current revision.

## Architecture and policy references

Contributions must preserve local-first privacy, declarative `.totlang`
packages, independence of generic projects from language-specific runtime
assemblies, and independence from SecondBrain. The governing context is:

- [repository architecture](README.md#project-structure);
- [community contribution, feedback, and curation architecture](docs/planning/community-contribution-feedback-curation-architecture.md);
- [content licensing policy](docs/content-licensing-policy.md);
- [Japanese pack rights ledger](TomeOfTongues.Language.Japanese/RIGHTS.md);
- [ADR 0012: community-maintained content](docs/architecture/adr/0012-community-maintained-language-pack-content.md);
- [ADR 0013: separate contribution rights](docs/architecture/adr/0013-separate-code-lesson-and-audio-contribution-rights.md);
- [ADR 0014: structured feedback](docs/architecture/adr/0014-structured-versioned-user-feedback.md);
- [ADR 0015: local-first feedback](docs/architecture/adr/0015-local-first-feedback-submission.md);
- [ADR 0016: AI-assisted, human-approved curation](docs/architecture/adr/0016-ai-assisted-human-approved-content-curation.md);
- [ADR 0017: provider-independent AI review](docs/architecture/adr/0017-provider-independent-ai-review-boundary.md); and
- [ADR 0018: human review and release gates](docs/architecture/adr/0018-content-review-states-and-release-gates.md).

Follow each linked document's recorded status. In particular, proposed ADRs
and declaration wording remain subject to human review.
