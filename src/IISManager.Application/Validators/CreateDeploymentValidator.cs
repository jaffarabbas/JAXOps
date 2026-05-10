using FluentValidation;
using IISManager.Application.DTOs;

namespace IISManager.Application.Validators;

public class CreateDeploymentValidator : AbstractValidator<CreateDeploymentDto>
{
    public CreateDeploymentValidator()
    {
        RuleFor(x => x.ApplicationId).GreaterThan(0);
        RuleFor(x => x.PackageId).GreaterThan(0);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50)
            .Matches(@"^[\w\.\-]+$").WithMessage("Version must contain only letters, digits, dots, and dashes");
        RuleFor(x => x.Targets).NotEmpty().WithMessage("At least one deployment target is required");
        RuleForEach(x => x.Targets).ChildRules(target =>
        {
            target.RuleFor(t => t.ServerId).GreaterThan(0);
            target.RuleFor(t => t.WebsiteName).NotEmpty().MaximumLength(200);
            target.RuleFor(t => t.AppPoolName).NotEmpty().MaximumLength(200);
            target.RuleFor(t => t.PhysicalPath).NotEmpty().MaximumLength(500);
        });
    }
}
