using FluentAssertions;
using FluentValidation.TestHelper;
using Splitr.Application.Commands.Auth;
using Splitr.Application.Validators;
using Xunit;

namespace Splitr.Tests.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPassValidation()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "SecureP@ss1");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEmail_ShouldFailValidation()
    {
        var command = new RegisterCommand("John Doe", "", "SecureP@ss1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void InvalidEmailFormat_ShouldFailValidation()
    {
        var command = new RegisterCommand("John Doe", "not-an-email", "SecureP@ss1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void ShortPassword_ShouldFailValidation()
    {
        var command = new RegisterCommand("John Doe", "john@example.com", "short");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void EmptyName_ShouldFailValidation()
    {
        var command = new RegisterCommand("", "john@example.com", "SecureP@ss1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameExceeds100Characters_ShouldFailValidation()
    {
        var longName = new string('A', 101);
        var command = new RegisterCommand(longName, "john@example.com", "SecureP@ss1");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
