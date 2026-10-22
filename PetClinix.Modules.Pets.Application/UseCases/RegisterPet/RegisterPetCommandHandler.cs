using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.Exceptions;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.RegisterPet;

public sealed class RegisterPetCommandHandler : ICommandHandler<RegisterPetCommand, Result>
{
    private readonly ITutorRepository _tutorRepository;
    private readonly IPetRepository _petRepository;
    private readonly IPetsUnitOfWork _unitOfWork;

    public RegisterPetCommandHandler(
        ITutorRepository tutorRepository,
        IPetRepository petRepository,
        IPetsUnitOfWork unitOfWork)
    {
        _tutorRepository = tutorRepository;
        _petRepository = petRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RegisterPetCommand command, CancellationToken cancellationToken)
    {
        var tutor = await _tutorRepository.GetByIdAsync(command.ClinicId, command.TutorId, cancellationToken);

        if (tutor == null)
        {
            return Result.Failure("pets.tutor.not_found", "Tutor não encontrado nesta clínica.");
        }

        if (await _petRepository.ExistsByNameAndTutorAsync(command.ClinicId, command.TutorId, command.Name, cancellationToken))
        {
            return Result.Failure("pets.pet.duplicate_name", "Já existe um pet com este nome cadastrado para este tutor.");
        }

        try
        {
            var pet = Pet.Create(
                command.ClinicId,
                command.TutorId,
                command.CreatedByUserId,
                command.Name,
                command.Species,
                command.Breed,
                command.BirthDate,
                command.Sex,
                command.Weight,
                command.IsNeutered,
                command.Notes
            );

            await _petRepository.AddAsync(pet, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (PetsDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}