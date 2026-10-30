using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Domain.Exceptions;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.SetPassword;

public sealed class SetPasswordCommandHandler : ICommandHandler<SetPasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public SetPasswordCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPasswordResetTokenAsync(command.Token, cancellationToken);

        if (user == null)
        {
            return Result.Failure("identity.user.invalid_token", "Token de redefinição inválido.");
        }

        if (!user.IsActive)
        {
            return Result.Failure("auth.inactive_account", "Esta conta está desativada.");
        }

        try
        {
            var passwordHash = _passwordHasher.Hash(command.Password);
            user.SetPassword(command.Token, passwordHash);

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (IdentityDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}