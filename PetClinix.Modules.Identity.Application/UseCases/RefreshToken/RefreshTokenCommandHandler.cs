using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.RefreshToken;

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(command.RefreshToken, cancellationToken);

        if (user == null || !user.IsValidRefreshToken(command.RefreshToken))
        {
            return Result<RefreshTokenResponse>.Failure("auth.invalid_refresh_token", "Refresh token inválido ou expirado.");
        }

        var newToken = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = user.GenerateRefreshToken();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(newToken, newRefreshToken));
    }
}