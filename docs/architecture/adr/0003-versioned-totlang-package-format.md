# ADR 0003: Versioned `.totlang` Package Format

- Status: Accepted
- Date: 2026-07-23

## Context

Curriculum, representations, audio, provenance, and compatibility need a
portable offline format.

## Decision

Use a ZIP-compatible `.totlang` artifact with a versioned JSON manifest,
course/lesson documents, assets, source ledger, license notices, and checksums.
The manifest records minimum engine version and declares only allow-listed
generic capabilities. Packs contain no executable assemblies.

Stable content IDs and per-item revisions preserve learner references across
updates.

Schema version 1 is represented by the language-neutral contracts in
`TomeOfTongues.Content.Schema`. Every JSON document declares
`"schemaVersion": 1`, uses camel-case property and enum names, rejects unknown
members, and preserves authored text without runtime transliteration or
language-specific code.

The v1 document set contains:

- a manifest with pack/package/engine versions, BCP 47 language and locale
  tags, localized display names, representation definitions, allow-listed
  normalization operations, course IDs, assets, sources, and licenses;
- a course catalog with stable course/unit/lesson IDs and per-item revisions;
- lesson documents with objectives, expressions, steps, and exercises;
- expression representations identified by language-neutral IDs, BCP 47 script
  tags, direction, authored text, range annotations, natural/literal meanings,
  audio references, and source references;
- generic exercise modalities, acceptable answers, assistance groups, scoring
  rules, and evidence mappings.

The contract deliberately has no score-based progression gate. Script
recognition can emit evidence but cannot require a correct result to advance.
Every spoken response and optional speaking opportunity must use
`deferredAllowed`, so speaking may be skipped or completed later without
blocking lesson progress.

`TomeOfTongues.Content.Tool.TotlangPackageTool` compiles and validates the
generic package boundary. An authoring directory contains `manifest.json`,
`courses.json`, `lessons/<lesson-id>.json`, and manifest-declared files below
`assets/`. The destination `.totlang` file must be outside that source
directory.

Compilation validates the complete source before replacing its destination,
then writes entries in stable ordinal order with stable ZIP timestamps.
Validation reopens the artifact and enforces:

- portable, unique paths and a declarative-only package with no executable
  entries;
- intrinsic JSON schema rules, including non-gating spoken work;
- manifest, catalog, unit, lesson, objective, expression, representation,
  source, license, and asset references;
- nonempty source origin, author, license, and attribution metadata, plus a
  nonempty name for every license notice;
- explicit redistribution permission for every source; and
- well-formed SHA-256 metadata and matching content for every declared asset.

The manifest itself carries the source ledger and license notices.

The first language-specific authoring boundary is
`TomeOfTongues.Language.Japanese`. Its checked-in `Source` directory contains
only declarative v1 documents, and its build emits
`artifacts/language-packs/tomeoftongues.japanese.totlang`. The initial scaffold
records original TomeOfTongues material under CC BY-SA 4.0 but contains no
lesson text or assets. `RIGHTS.md` records the content-owner workflow for
future authored text and human audio.

At runtime,
`TomeOfTongues.Infrastructure.Packaging.TotlangPackageCatalog` discovers
installed artifacts from an app-private filesystem root. Installation first
copies to a non-discoverable temporary file, applies the same complete Content
validation used by the authoring tool, and rejects packs whose declared
minimum engine version is newer than the running engine. A successful install
is atomically moved to a deterministic SHA-256 identity path, so manifest
values cannot escape the catalog root and a failed reinstall preserves the
previous artifact. Discovery revalidates artifacts without loading executable
assemblies, is deterministic by pack ID and package version, and survives a
process restart without requiring SQLite metadata.

## Consequences

Packs can be validated before installation, installed offline, backed up, and
updated independently. Schema migrations and compatibility windows must be
explicit.

The generic engine can deserialize declarative data without referencing or
loading a language project or executable assembly. Unsupported versions,
unknown members, malformed JSON, invalid annotation ranges, and gating spoken
work fail at the schema boundary.

## Verification

```powershell
dotnet test tests/TomeOfTongues.Content.Tests/TomeOfTongues.Content.Tests.csproj
dotnet test tests/TomeOfTongues.Content.Tool.Tests/TomeOfTongues.Content.Tool.Tests.csproj
dotnet test tests/TomeOfTongues.Infrastructure.Tests/TomeOfTongues.Infrastructure.Tests.csproj
dotnet run --project TomeOfTongues.Content.Tool/TomeOfTongues.Content.Tool.csproj -- validate artifacts/language-packs
dotnet test tests/TomeOfTongues.Language.Japanese.Tests/TomeOfTongues.Language.Japanese.Tests.csproj
dotnet test tests/TomeOfTongues.Architecture.Tests/TomeOfTongues.Architecture.Tests.csproj
```

## Rejected alternatives

- Database files as distribution packages.
- Unversioned loose JSON.
- Executable language extensions.
