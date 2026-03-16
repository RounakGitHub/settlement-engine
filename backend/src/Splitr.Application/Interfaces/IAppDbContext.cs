using Microsoft.EntityFrameworkCore;
using Splitr.Domain.Entities;

namespace Splitr.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMember> GroupMembers { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseSplit> ExpenseSplits { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<StoredEvent> StoredEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
