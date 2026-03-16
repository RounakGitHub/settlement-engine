namespace Splitr.Domain.Enums;

public enum EventType
{
    ExpenseAdded,
    ExpenseEdited,
    ExpenseDeleted,
    SettlementProposed,
    SettlementConfirmed,
    SettlementExpired,
    GroupCreated,
    MemberJoined,
    DebtGraphUpdated,
    SettlementCancelled,
    MemberLeft,
    GroupArchived,
    SettlementFailed
}
