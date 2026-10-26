using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.DeactivateTutor;

public sealed class DeactivateTutorCommandHandler : ICommandHandler<DeactivateTutorCommand, Result>
{
    private readonly ITutorRepository _tutorRepository;
    private readonly IPetsUnitOfWork _unitOfWork;
    private readonly IPetDependencyChecker _petDependencyChecker;
    private readonly IAppointmentDependencyChecker _appointmentDependencyChecker;

    public DeactivateTutorCommandHandler(
        ITutorRepository tutorRepository,
        IPetsUnitOfWork unitOfWork,
        IPetDependencyChecker petDependencyChecker,
        IAppointmentDependencyChecker appointmentDependencyChecker)
    {
        _tutorRepository = tutorRepository;
        _unitOfWork = unitOfWork;
        _petDependencyChecker = petDependencyChecker;
        _appointmentDependencyChecker = appointmentDependencyChecker;
    }

    public async Task<Result> Handle(DeactivateTutorCommand command, CancellationToken cancellationToken)
    {
        var tutor = await _tutorRepository.GetByIdAsync(command.ClinicId, command.TutorId, cancellationToken);

        if (tutor == null)
        {
            return Result.Failure("pets.tutor.not_found", "Tutor não encontrado nesta clínica.");
        }

        if (await _petDependencyChecker.HasActivePetsByTutorAsync(command.ClinicId, tutor.Id, cancellationToken))
        {
            return Result.Failure("pets.tutor.has_active_pets", "Não é possível desativar o tutor pois existem Pets ativos vinculados a ele. Desative ou transfira os pets primeiro.");
        }

        if (await _appointmentDependencyChecker.HasFutureAppointmentsForTutorAsync(command.ClinicId, tutor.Id, cancellationToken))
        {
            return Result.Failure("pets.tutor.has_future_appointments", "Não é possível desativar o tutor pois existem agendamentos futuros vinculados a ele. Cancele ou conclua os agendamentos primeiro.");
        }

        tutor.Deactivate();

        await _tutorRepository.UpdateAsync(tutor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}