namespace TomeOfTongues.Core.Exercises;

public readonly record struct ResponseLatency
{
    public ResponseLatency(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Response latency cannot be negative.");
        }

        Value = value;
    }

    public TimeSpan Value { get; }
}
