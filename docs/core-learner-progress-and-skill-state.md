# Core learner progress and skill state

`TomeOfTongues.Core.Progress` owns two language-neutral learner-state models.
They have no dependency on Content, Infrastructure, MAUI, SecondBrain, or a
language-specific project.

`LearnerProgress` records curriculum facts only: enrollment, prerequisite-based
lesson unlocks, completed lessons, the last lesson/step position, and
timestamps. Enrollment initially unlocks every lesson with no prerequisites.
Completing an unlocked lesson unlocks any lesson whose authored prerequisites
are now complete. It does not inspect attempts, evidence, scores, scripts, or
speaking results.

`LearnerSkillState` records the ordered evidence observations for exactly one
objective, skill dimension, and optional representation. Evidence must match
that identity, remain chronological, and cannot be recorded twice. The state
preserves measured, self-reported, and estimated observations without reducing
them to mastery, proficiency, or a global score.

Both models expose immutable snapshots for persistence and restart. Progress
restoration requires the exact course revision and validates completed lessons,
derived unlocks, prerequisites, and the last authored position. Skill-state
restoration validates identity, uniqueness, chronology, and its latest update
time. SQLite implementation remains owned exclusively by Infrastructure.

Curriculum completion and skill evidence intentionally remain independent.
Incorrect script-recognition evidence cannot lock a lesson. Spoken production
may be self-reported without speech recognition, and an incorrect or deferred
speaking result cannot prevent curriculum progression.

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
