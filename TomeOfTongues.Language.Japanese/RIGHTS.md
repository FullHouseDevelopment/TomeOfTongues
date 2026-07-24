# Japanese pack rights ledger

The machine-readable rights ledger is the `sources` and `licenses` data in
`Source/manifest.json`. The source record covers the six original practical
starter lessons authored specifically for TomeOfTongues and released under
CC BY-SA 4.0. The lessons cover greetings, introductions, courtesy, simple
requests, directions, and everyday transactions. They contain no copied
course material, audio, images, or private imports.

Every future lesson expression or bundled asset must reference a source entry.
Before adding one, record its origin, author, reviewer when applicable,
license, attribution, redistribution and modification permissions, and record
date. Bundled assets must also declare and match their SHA-256 checksum.

External protected courses remain link-only unless their exact license permits
redistribution. Human-recorded audio requires an explicit contributor grant
covering editing, packaging, redistribution, and attribution. The starter
lessons remain preview content until a competent Japanese reviewer has checked
their Japanese wording, English meanings, and romanization. Any future audio
remains subject to the same review and contributor-grant rules.

Validate the generated artifact with:

```powershell
dotnet build TomeOfTongues.Language.Japanese/TomeOfTongues.Language.Japanese.csproj
dotnet run --project TomeOfTongues.Content.Tool/TomeOfTongues.Content.Tool.csproj -- validate artifacts/language-packs
dotnet test tests/TomeOfTongues.Language.Japanese.Tests/TomeOfTongues.Language.Japanese.Tests.csproj
dotnet test tests/TomeOfTongues.Architecture.Tests/TomeOfTongues.Architecture.Tests.csproj
```
