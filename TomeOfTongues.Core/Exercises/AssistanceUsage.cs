using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.Exercises;

public sealed record AssistanceUsage
{
    public AssistanceUsage(string groupId, AssistanceKind kind)
    {
        GroupId = DomainGuard.Identifier(groupId, nameof(groupId));

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
    }

    public string GroupId { get; }

    public AssistanceKind Kind { get; }
}
