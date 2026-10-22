using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Exceptions;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.UpdatePet;

public sealed class UpdatePetCommandHandler : ICommandHandler<UpdatePetCommand, Result>
{
    private readonly IPetRepository _petRepository;
    private readonly IPetsUnitOfWork _unitOfWork;

    public UpdatePetCommandHandler(IPetRepository petRepository, IPetsUnitOfWork unitOfWork)
    {
        _petRepository = petRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdatePetCommand command, CancellationToken cancellationToken)
    {
        var pet = await _petRepository.GetByIdAsync(command.ClinicId, command.PetId, cancellationToken);
        
        if (pet == null || pet.TutorId != command.TutorId)
        {
            return Result.Failure("pets.pet.not_found", "Pet não encontrado para este tutor.");
        }

        try
        {
            pet.UpdateInfo(
                command.UpdatedByUserId,
                command.Name,
                command.Species,
                command.Breed,
                command.BirthDate,
                command.Sex,
                command.Weight,
                command.IsNeutered,
                command.Notes);

            await _petRepository.UpdateAsync(pet, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (PetsDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}