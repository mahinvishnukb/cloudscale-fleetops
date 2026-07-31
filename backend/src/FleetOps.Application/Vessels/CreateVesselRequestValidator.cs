using FluentValidation;
using FleetOps.Domain.Vessels;

namespace FleetOps.Application.Vessels;

public sealed class CreateVesselRequestValidator : AbstractValidator<CreateVesselRequest>
{
    public CreateVesselRequestValidator()
    {
        RuleFor(x => x.ImoNumber)
            .NotEmpty().WithMessage("IMO number is required.")
            .Must(imo => ImoNumber.TryCreate(imo, out _))
            .WithMessage("IMO number must be 7 digits with a valid check digit.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.HomePort)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.GrossTonnage)
            .InclusiveBetween(1, 300_000);

        RuleFor(x => x.Type)
            .NotEqual(VesselType.Unknown).WithMessage("Select a vessel type.");
    }
}
