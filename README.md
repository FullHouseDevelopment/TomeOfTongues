# TomeOfTongues

TomeOfTongues is the technical name for the local-first, language-neutral
learning engine displayed as **Tome of Many Tongues**.

## Project structure

- `TomeOfTongues.Core` contains domain types and policies and has no project
  dependencies.
- `TomeOfTongues.Application` contains use cases and ports and depends only on
  Core.
- `TomeOfTongues.Content` contains generic `.totlang` contracts and tooling and
  depends only on Core.
- `TomeOfTongues.Content.Tool` is the generic content-tooling boundary and
  depends only on Content.
- `TomeOfTongues.Infrastructure` owns persistence and external adapters and
  depends on Core, Application, and Content.
- `TomeOfTongues.Maui` is the Android-first MAUI host boundary and depends only
  on Application and Infrastructure.
- `tests/` mirrors the generic production projects and includes executable
  dependency-boundary tests.

Language projects are independent authoring projects that emit declarative
`.totlang` artifacts. Generic production and test projects must not reference a
`TomeOfTongues.Language.*` project, assembly, namespace, or type.
Architecture tests enforce that boundary across C# source, project files, and
shared MSBuild `.props` and `.targets` inputs while leaving declarative
`.totlang` content outside the executable dependency scan.

The MAUI host remains outside `TomeOfTongues.NonMaui.slnf`, allowing generic
projects and tests to build without installing a MAUI workload.

## Verification

Install the .NET 10 SDK. The repository selects the 10.0.200 feature band and
accepts its latest servicing patch. Then run:

```powershell
dotnet restore TomeOfTongues.NonMaui.slnf
dotnet build TomeOfTongues.NonMaui.slnf --configuration Debug --no-restore
dotnet test TomeOfTongues.NonMaui.slnf --configuration Debug --no-build --no-restore
```

For the Android MAUI boundary, install or restore the Android workload and run
the direct project build:

```powershell
dotnet workload restore TomeOfTongues.Maui/TomeOfTongues.Maui.csproj
dotnet build TomeOfTongues.Maui/TomeOfTongues.Maui.csproj --configuration Debug --framework net10.0-android
```
