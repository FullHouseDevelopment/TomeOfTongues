using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.Exercises;

public readonly record struct RepresentationId
{
    public RepresentationId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}
