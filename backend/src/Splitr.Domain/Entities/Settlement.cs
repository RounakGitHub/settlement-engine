using Splitr.Domain.Enums;

namespace Splitr.Domain.Entities;

public class Settlement : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid PayerId { get; set; }
    public Guid PayeeId { get; set; }
    public long AmountPaise { get; set; }
    public SettlementStatus Status { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
