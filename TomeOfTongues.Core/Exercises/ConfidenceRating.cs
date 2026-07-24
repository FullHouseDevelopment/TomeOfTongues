namespace TomeOfTongues.Core.Exercises;

public readonly record struct ConfidenceRating
{
    public ConfidenceRating(int value)
    {
        if (value is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Confidence must be between 1 and 5.");
        }

        Value = value;
    }

    public int Value { get; }
}
