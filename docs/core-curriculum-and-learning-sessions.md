# Core curriculum and learning sessions

`TomeOfTongues.Core` owns the language-neutral curriculum and learning-session
domain model. It has no dependency on Content, Infrastructure, MAUI,
SecondBrain, or a language-specific project.

Curriculum definitions are immutable and ordered. Stable typed identifiers and
positive revisions identify courses, units, lessons, objectives, and steps.
Course construction validates duplicate identifiers, lesson prerequisite
references, and prerequisite cycles. Content loaders may map declarative
`.totlang` data into these types without reversing the Core dependency.

A learning session targets an exact course, lesson, and lesson revision. Its
state records completed, skipped, and deferred step dispositions separately,
along with the current authored position, timestamps, and the session's Silent
Mode snapshot. A snapshot can be persisted by a later Infrastructure outcome
and restored only when:

- it targets the same course and lesson revision;
- terminal dispositions are mutually exclusive and form a contiguous prefix;
- skipped and deferred dispositions are permitted by their steps; and
- its current position and status agree with those dispositions.

Completion advances through authored curriculum; it does not record or infer
competence. Optional speaking opportunities must allow deferral, and deferring
them completes their curriculum position without spoken evidence. Script or
speaking success therefore cannot gate this session model.

## Verification

Run the focused Core tests:

```powershell
dotnet test tests/TomeOfTongues.Core.Tests/TomeOfTongues.Core.Tests.csproj
```

Run the complete non-MAUI verification:

```powershell
dotnet restore TomeOfTongues.NonMaui.slnf
dotnet build TomeOfTongues.NonMaui.slnf --configuration Debug --no-restore
dotnet test TomeOfTongues.NonMaui.slnf --configuration Debug --no-build --no-restore
```
