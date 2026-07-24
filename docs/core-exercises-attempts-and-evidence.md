# Core exercises, attempts, and multidimensional evidence

`TomeOfTongues.Core.Exercises` owns the language-neutral runtime model for
exercise definitions, learner attempts, and the evidence emitted by evaluated
attempts. It has no dependency on Content, Infrastructure, MAUI, SecondBrain,
or a language-specific project.

An exercise definition identifies its revision, prompt and response modalities,
and authored evidence mappings. Each mapping targets one objective and skill
dimension, with an optional representation identifier. Duplicate
objective/skill/representation mappings are rejected so an attempt cannot
silently double-count the same observation.

An attempt is an immutable record of:

- its exercise, session, item revision, and pack revision;
- the prompt and response modalities seen by the learner;
- Silent Mode, raw local response, and assistance conditions;
- response latency, confidence, outcome, and timestamps; and
- correctness and whether the result was measured, self-reported, or estimated.

Evaluated attempts emit one independently identified evidence observation for
each authored mapping. Every observation preserves its objective, skill
dimension, optional representation, modalities, assistance, correctness,
measurement type, latency, confidence, timestamp, and revision context.
Persistence can store and reconstruct this immutable property graph without
deriving competence from curriculum state.

Skipped or deferred attempts emit no competence evidence. A spoken response may
emit explicitly self-reported evidence without speech recognition. Recording
correct, incorrect, script-targeted, or spoken evidence does not advance a
learning session; curriculum position remains owned by the separate learning
session model. Exposure, lesson completion, and multidimensional competence
evidence therefore remain distinct facts.

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
