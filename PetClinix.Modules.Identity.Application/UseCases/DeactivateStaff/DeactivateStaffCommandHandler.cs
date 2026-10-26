using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.DeactivateStaff;

public sealed class DeactivateStaffCommandHandler : ICommandHandler<DeactivateStaffCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppointmentDependencyChecker _appointmentDependencyChecker;

    public DeactivateStaffCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAppointmentDependencyChecker appointmentDependencyChecker)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _appointmentDependencyChecker = appointmentDependencyChecker;
    }

    public async Task<Result> Handle(DeactivateStaffCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user == null || user.ClinicId != command.ClinicId)
        {
            return Result.Failure("identity.user.not_found", "Funcionário não encontrado nesta clínica.");
        }

        if (user.Role == UserRole.Admin)
        {
            return Result.Failure("identity.user.cannot_deactivate_admin", "Não é possível desativar o perfil do Admin por esta rota.");
        }

        if (await _appointmentDependencyChecker.HasFutureAppointmentsForVetAsync(command.ClinicId, user.Id, cancellationToken))
        {
            return Result.Failure("identity.user.has_future_appointments", "Não é possível desativar o profissional pois existem agendamentos futuros vinculados a ele.");
        }

        user.Deactivate();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}