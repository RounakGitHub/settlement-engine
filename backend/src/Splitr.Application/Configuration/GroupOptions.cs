namespace Splitr.Application.Configuration;

public sealed class GroupOptions
{
    public const string SectionName = "Group";

    public required int MaxMembers { get; init; }
    public required int ArchiveRetentionDays { get; init; }
    public required int InviteCodeLength { get; init; }
}
