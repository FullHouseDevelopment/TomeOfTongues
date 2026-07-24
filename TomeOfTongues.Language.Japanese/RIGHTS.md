# Japanese pack rights ledger

The machine-readable rights ledger is the `sources` and `licenses` data in
`Source/manifest.json`. The initial source record covers only original
Japanese material authored specifically for TomeOfTongues and released under
CC BY-SA 4.0. This structural preview contains no lesson text, audio, images,
copied course material, or private imports.

Every future lesson expression or bundled asset must reference a source entry.
Before adding one, record its origin, author, reviewer when applicable,
license, attribution, redistribution and modification permissions, and record
date. Bundled assets must also declare and match their SHA-256 checksum.

External protected courses remain link-only unless their exact license permits
redistribution. Human-recorded audio requires an explicit contributor grant
covering editing, packaging, redistribution, and attribution. Until a
competent Japanese reviewer has checked authored content and audio, package
versions remain preview releases.

Validate the generated artifact with:

```powershell
dotnet build TomeOfTongues.Language.Japanese/TomeOfTongues.Language.Japanese.csproj
dotnet run --project TomeOfTongues.Content.Tool/TomeOfTongues.Content.Tool.csproj -- validate artifacts/language-packs
```
