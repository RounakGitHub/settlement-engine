using FluentValidation;
using Splitr.Application.Commands.Expenses;

namespace Splitr.Application.Validators;

public class AddExpenseCommandValidator : AbstractValidator<AddExpenseCommand>
{
    public AddExpenseCommandValidator()
    {
        RuleFor(x => x.AmountPaise)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Splits)
            .NotEmpty().WithMessage("At least one split is required.");

        RuleForEach(x => x.Splits)
            .ChildRules(split =>
            {
                split.RuleFor(s => s.AmountPaise)
                    .GreaterThanOrEqualTo(0).WithMessage("Split amount must not be negative.");
            });

        RuleFor(x => x)
            .Must(x => Math.Abs(x.Splits.Sum(s => s.AmountPaise) - x.AmountPaise) <= 1)
            .WithMessage("Sum of splits must equal the total amount (tolerance of 1 paisa).");
    }
}
