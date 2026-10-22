using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Catalog.Application.Contracts;
using PetClinix.Modules.Catalog.Domain.Repositories;

namespace PetClinix.Modules.Catalog.Application.UseCases.DeactivateService;

public sealed class DeactivateServiceCommandHandler : ICommandHandler<DeactivateServiceCommand, Result>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ICatalogUnitOfWork _unitOfWork;
    private readonly IAppointmentDependencyChecker _appointmentDependencyChecker;

    public DeactivateServiceCommandHandler(
        IServiceRepository serviceRepository,
        ICatalogUnitOfWork unitOfWork,
        IAppointmentDependencyChecker appointmentDependencyChecker)
    {
        _serviceRepository = serviceRepository;
        _unitOfWork = unitOfWork;
        _appointmentDependencyChecker = appointmentDependencyChecker;
    }

    public async Task<Result> Handle(DeactivateServiceCommand command, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(command.ClinicId, command.ServiceId, cancellationToken);

        if (service == null)
        {
            return Result.Failure("catalog.service.not_found", "Serviço não encontrado nesta clínica.");
        }

        if (await _appointmentDependencyChecker.HasFutureAppointmentsForServiceAsync(service.Id, cancellationToken))
        {
            return Result.Failure("catalog.service.has_future_appointments", "Não é possível desativar o serviço pois existem agendamentos futuros vinculados a ele.");
        }

        service.Deactivate();

        await _serviceRepository.UpdateAsync(service, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}