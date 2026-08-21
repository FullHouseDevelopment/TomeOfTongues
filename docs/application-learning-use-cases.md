# Application learning use cases

`TomeOfTongues.Application.Learning` owns the language-neutral orchestration
between the Core learning aggregates and future persistence adapters. It
depends only on `TomeOfTongues.Core`; it does not query SQLite, load a
language-specific runtime assembly, or depend on MAUI or SecondBrain.

## Use cases

`StartOrResumeLessonUseCase` loads a curriculum and learner progress, rejects a
locked lesson, resumes its active session when one exists, or atomically
persists a new session and enrollment position. The requested session ID and
Silent Mode value are used only when a new session is created.

`SubmitLearningStepUseCase` resolves the exercise for the current step, records
an attempt and its evidence, updates the matching learner skill states, applies
the Core step transition, and updates curriculum progress. Incorrect
script-recognition evidence does not block the step transition. A deferred
speaking opportunity completes its step without producing spoken evidence and
does not block lesson completion.

## Ports and transactions

Application owns explicit ports for curriculum and exercise lookup, learning
sessions, curriculum progress, attempts, and skill states. Infrastructure
implements those ports later; callers never receive database paths or issue
SQL.

Every use-case call executes through `ILearningUnitOfWork`. The transaction
provides transaction-scoped repository instances and commits only when the
operation returns successfully. Cancellation, validation errors, and repository
failures roll back the complete operation so attempts, evidence, sessions, and
progress cannot diverge.

## Verification

From the repository root:

```powershell
dotnet restore TomeOfTongues.NonMaui.slnf
dotnet build TomeOfTongues.NonMaui.slnf --configuration Debug --no-restore
dotnet test TomeOfTongues.NonMaui.slnf --configuration Debug --no-build --no-restore
```
