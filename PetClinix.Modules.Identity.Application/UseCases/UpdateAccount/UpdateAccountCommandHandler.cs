using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Domain.Exceptions;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.UpdateAccount;

public sealed class UpdateAccountCommandHandler : ICommandHandler<UpdateAccountCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IClinicRepository _clinicRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UpdateAccountCommandHandler(
        IUserRepository userRepository,
        IClinicRepository clinicRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _clinicRepository = clinicRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result> Handle(UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        var clinic = await _clinicRepository.GetByIdAsync(command.ClinicId, cancellationToken);

        if (user == null)
            return Result.Failure("identity.user.not_found", "Usuário não encontrado.");

        if (clinic == null)
            return Result.Failure("identity.clinic.not_found", "Clínica não encontrada.");

        try
        {
            user.UpdatePersonalInfo(command.UserName, command.UserPhoneNumber, command.UserBirthDate, command.UserId);

            if (!string.IsNullOrWhiteSpace(command.NewPassword))
            {
                var passwordHash = _passwordHasher.Hash(command.NewPassword);
                user.ChangePassword(passwordHash);
            }

            clinic.UpdateInfo(
                command.ClinicTradeName, command.ClinicLegalName, command.ClinicDocumentNumber,
                command.ClinicEmail, command.ClinicPhoneNumber,
                command.ClinicZipCode, command.ClinicStreet, command.ClinicNumber, command.ClinicNeighborhood,
                command.ClinicComplement, command.ClinicCity, command.ClinicState);

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _clinicRepository.UpdateAsync(clinic, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (IdentityDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}