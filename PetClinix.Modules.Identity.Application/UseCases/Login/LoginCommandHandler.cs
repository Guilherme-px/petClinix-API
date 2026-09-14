using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Domain.Repositories;
using PetClinix.Modules.Identity.Domain.ValueObjects;

namespace PetClinix.Modules.Identity.Application.UseCases.Login;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionStatusService _subscriptionStatusService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        ISubscriptionStatusService subscriptionStatusService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
        _subscriptionStatusService = subscriptionStatusService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var emailVo = Email.Create(command.Email);
        var user = await _userRepository.GetByEmailAsync(emailVo, cancellationToken);

        if (user == null || user.PasswordHash == null)
        {
            return Result<LoginResponse>.Failure("auth.invalid_credentials", "Usuário ou senha inválidos.");
        }

        var isPasswordValid = _passwordHasher.Verify(command.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            return Result<LoginResponse>.Failure("auth.invalid_credentials", "Usuário ou senha inválidos.");
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Failure("auth.inactive_account", "Esta conta está desativada.");
        }

        var isClinicActive = await _subscriptionStatusService.IsClinicActiveAsync(user.ClinicId, cancellationToken);
        if (!isClinicActive)
        {
            return Result<LoginResponse>.Failure("auth.subscription_inactive", "A assinatura da clínica está inativa ou cancelada. Acesse o portal para reativar.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = user.GenerateRefreshToken();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(token, refreshToken, user.Email.Value, user.Role.ToString(), user.Name));
    }
}