namespace Splitr.Domain.Entities;

public class ExpenseSplit : BaseEntity
{
    public Guid ExpenseId { get; set; }
    public Guid UserId { get; set; }
    public long AmountPaise { get; set; }
}
