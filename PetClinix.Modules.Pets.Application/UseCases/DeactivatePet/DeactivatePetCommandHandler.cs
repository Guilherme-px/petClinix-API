using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.DeactivatePet;

public sealed class DeactivatePetCommandHandler : ICommandHandler<DeactivatePetCommand, Result>
{
    private readonly IPetRepository _petRepository;
    private readonly IPetsUnitOfWork _unitOfWork;
    private readonly IAppointmentDependencyChecker _appointmentDependencyChecker;

    public DeactivatePetCommandHandler(
        IPetRepository petRepository,
        IPetsUnitOfWork unitOfWork,
        IAppointmentDependencyChecker appointmentDependencyChecker)
    {
        _petRepository = petRepository;
        _unitOfWork = unitOfWork;
        _appointmentDependencyChecker = appointmentDependencyChecker;
    }

    public async Task<Result> Handle(DeactivatePetCommand command, CancellationToken cancellationToken)
    {
        var pet = await _petRepository.GetByIdAsync(command.ClinicId, command.PetId, cancellationToken);

        if (pet == null || pet.TutorId != command.TutorId)
        {
            return Result.Failure("pets.pet.not_found", "Pet não encontrado para este tutor.");
        }

        if (await _appointmentDependencyChecker.HasFutureAppointmentsForPetAsync(command.ClinicId, pet.Id, cancellationToken))
        {
            return Result.Failure("pets.pet.has_future_appointments", "Não é possível desativar o pet pois existem agendamentos futuros vinculados a ele.");
        }

        pet.Deactivate();

        await _petRepository.UpdateAsync(pet, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}   