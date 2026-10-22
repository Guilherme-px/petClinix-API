using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Exceptions;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.UpdateTutor;

public sealed class UpdateTutorCommandHandler : ICommandHandler<UpdateTutorCommand, Result>
{
    private readonly ITutorRepository _tutorRepository;
    private readonly IPetsUnitOfWork _unitOfWork;

    public UpdateTutorCommandHandler(ITutorRepository tutorRepository, IPetsUnitOfWork unitOfWork)
    {
        _tutorRepository = tutorRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateTutorCommand command, CancellationToken cancellationToken)
    {
        var tutor = await _tutorRepository.GetByIdAsync(command.ClinicId, command.TutorId, cancellationToken);

        if (tutor == null)
        {
            return Result.Failure("pets.tutor.not_found", "Tutor não encontrado nesta clínica.");
        }

        try
        {
            tutor.UpdateInfo(
                command.Name, command.Email, command.PhoneNumber, command.SecondaryPhoneNumber,
                command.ZipCode, command.Street, command.Number, command.Neighborhood,
                command.Complement, command.City, command.State, command.Notes
            );

            await _tutorRepository.UpdateAsync(tutor, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (PetsDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}