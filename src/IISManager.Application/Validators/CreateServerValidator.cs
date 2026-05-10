using FluentValidation;
using IISManager.Application.DTOs;

namespace IISManager.Application.Validators;

public class CreateServerValidator : AbstractValidator<CreateServerDto>
{
    public CreateServerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IpAddress).NotEmpty().MaximumLength(45);
        RuleFor(x => x.Environment).NotEmpty()
            .Must(e => new[] { "Development", "Staging", "Production", "UAT" }.Contains(e))
            .WithMessage("Environment must be Development, Staging, UAT, or Production");
    }
}
