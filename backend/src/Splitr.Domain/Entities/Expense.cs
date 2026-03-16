using Splitr.Domain.Enums;

namespace Splitr.Domain.Entities;

public class Expense : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid PaidBy { get; set; }
    public long AmountPaise { get; set; }
    public string Description { get; set; } = string.Empty;
    public SplitType SplitType { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<ExpenseSplit> Splits { get; set; } = [];
}
