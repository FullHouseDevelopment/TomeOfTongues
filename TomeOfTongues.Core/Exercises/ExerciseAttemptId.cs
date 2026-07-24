using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.Exercises;

public readonly record struct ExerciseAttemptId
{
    public ExerciseAttemptId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}
