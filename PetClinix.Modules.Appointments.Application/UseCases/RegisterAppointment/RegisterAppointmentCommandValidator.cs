using FluentValidation;

namespace PetClinix.Modules.Appointments.Application.UseCases.RegisterAppointment;

public sealed class RegisterAppointmentCommandValidator : AbstractValidator<RegisterAppointmentCommand>
{
    public RegisterAppointmentCommandValidator()
    {
        RuleFor(x => x.TutorId).NotEmpty();
        RuleFor(x => x.PetId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.VeterinarianId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Time).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}